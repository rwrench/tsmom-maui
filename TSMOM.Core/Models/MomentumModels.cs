namespace TSMOM.Core.Models;

public class MomentumResponse
{
    public string Ticker { get; set; } = string.Empty;
    public double? Momentum { get; set; }
    public double? StartPrice { get; set; }
    public double? EndPrice { get; set; }
    public double? PercentChange { get; set; }
    public bool IsBuyCandidate { get; set; }
    public string? Error { get; set; }
}

public class BatchMomentumResponse
{
    public List<MomentumResponse> Results { get; set; } = new();
    public string? BestBuy { get; set; }
    public string? BestSell { get; set; }
}

public class PriceTick
{
    public DateTime Date { get; set; }
    public double Close { get; set; }
}
