using TSMOM.Core.Models;

namespace TSMOM.Core.Services;

public interface IStockDataProvider
{
    Task<(List<PriceTick>? Prices, string? Error)> GetPriceHistoryAsync(
        string ticker, DateTime startDate, DateTime endDate);
}
