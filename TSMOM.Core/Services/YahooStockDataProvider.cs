using NodaTime;
using YahooQuotesApi;
using TSMOM.Core.Models;

namespace TSMOM.Core.Services;

public class YahooStockDataProvider : IStockDataProvider
{
    private const int TimeoutSeconds = 15;
    private const int MaxRetries = 3;

    public async Task<(List<PriceTick>? Prices, string? Error)> GetPriceHistoryAsync(
        string ticker, DateTime startDate, DateTime endDate)
    {
        Exception? lastException = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));

                var result = await FetchWithTimeoutAsync(ticker, startDate, endDate, cts.Token);
                return result;
            }
            catch (OperationCanceledException)
            {
                lastException = new TimeoutException($"Request timed out after {TimeoutSeconds}s");
                if (attempt < MaxRetries)
                    await Task.Delay(1000); // Brief delay before retry
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt < MaxRetries)
                    await Task.Delay(1000);
            }
        }

        return (null, $"Failed after {MaxRetries} attempts: {lastException?.Message}");
    }

    private async Task<(List<PriceTick>? Prices, string? Error)> FetchWithTimeoutAsync(
        string ticker, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        var startInstant = Instant.FromUtc(startDate.Year, startDate.Month, startDate.Day, 0, 0);

        var yahooQuotes = new YahooQuotesBuilder()
            .WithHistoryStartDate(startInstant)
            .Build();

        var task = yahooQuotes.GetHistoryAsync(ticker);
        var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken));

        if (completedTask != task)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        var result = await task;

        if (result.HasError)
        {
            return (null, result.Error.Message ?? $"Failed to fetch data for {ticker}");
        }

        var prices = result.Value.Ticks
            .Where(t => t.Date.ToDateTimeUtc() <= endDate)
            .Select(t => new PriceTick
            {
                Date = t.Date.ToDateTimeUtc(),
                Close = (double)t.Close
            })
            .ToList();

        return (prices, null);
    }
}
