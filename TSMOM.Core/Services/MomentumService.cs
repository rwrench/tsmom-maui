using TSMOM.Core.Models;

namespace TSMOM.Core.Services;

public class MomentumService
{
    private readonly IStockDataProvider _stockDataProvider;

    public MomentumService(IStockDataProvider stockDataProvider)
    {
        _stockDataProvider = stockDataProvider;
    }

    public async Task<MomentumResponse> CalculateMomentumAsync(
        string ticker, DateTime startDate, DateTime endDate)
    {
        var symbol = ticker.Trim().ToUpper();

        var (prices, error) = await _stockDataProvider.GetPriceHistoryAsync(symbol, startDate, endDate);

        if (error != null)
        {
            return new MomentumResponse
            {
                Ticker = symbol,
                Error = error
            };
        }

        if (prices == null || prices.Count < 2)
        {
            return new MomentumResponse
            {
                Ticker = symbol,
                Error = "Insufficient price data for the selected date range"
            };
        }

        var startPrice = prices.First().Close;
        var endPrice = prices.Last().Close;
        var momentum = endPrice - startPrice;
        var percentChange = ((endPrice - startPrice) / startPrice) * 100;

        return new MomentumResponse
        {
            Ticker = symbol,
            Momentum = momentum,
            StartPrice = startPrice,
            EndPrice = endPrice,
            PercentChange = percentChange,
            IsBuyCandidate = momentum > 0
        };
    }

    public async Task<BatchMomentumResponse> CalculateBatchMomentumAsync(
        List<string> tickers, DateTime startDate, DateTime endDate)
    {
        // Limit concurrent requests to avoid API throttling
        const int maxConcurrency = 3;
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        var tasks = tickers.Select(async t =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await CalculateMomentumAsync(t, startDate, endDate);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);

        var validResults = results
            .Where(r => r.Error == null && r.Momentum.HasValue)
            .ToList();

        string? bestBuy = null;
        string? bestSell = null;
        if (validResults.Any())
        {
            bestBuy = validResults
                .OrderByDescending(r => r.PercentChange)
                .First()
                .Ticker;

            bestSell = validResults
                .OrderBy(r => r.PercentChange)
                .First()
                .Ticker;
        }

        return new BatchMomentumResponse
        {
            Results = results.ToList(),
            BestBuy = bestBuy,
            BestSell = bestSell
        };
    }
}
