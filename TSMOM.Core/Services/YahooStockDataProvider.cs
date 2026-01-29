using NodaTime;
using YahooQuotesApi;
using TSMOM.Core.Models;

namespace TSMOM.Core.Services;

public class YahooStockDataProvider : IStockDataProvider
{
    public async Task<(List<PriceTick>? Prices, string? Error)> GetPriceHistoryAsync(
        string ticker, DateTime startDate, DateTime endDate)
    {
        try
        {
            var startInstant = Instant.FromUtc(startDate.Year, startDate.Month, startDate.Day, 0, 0);

            var yahooQuotes = new YahooQuotesBuilder()
                .WithHistoryStartDate(startInstant)
                .Build();

            var result = await yahooQuotes.GetHistoryAsync(ticker);

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
        catch (Exception ex)
        {
            return (null, $"Failed to fetch data: {ex.Message}");
        }
    }
}
