using Moq;
using TSMOM.Core.Models;
using TSMOM.Core.Services;

namespace TSMOM.Maui.Tests;

public class MomentumServiceTests
{
    private readonly Mock<IStockDataProvider> _mockProvider;
    private readonly MomentumService _service;

    public MomentumServiceTests()
    {
        _mockProvider = new Mock<IStockDataProvider>();
        _service = new MomentumService(_mockProvider.Object);
    }

    [Fact]
    public async Task CalculateMomentumAsync_WithPositiveMomentum_ReturnsBuyCandidate()
    {
        // Arrange
        var prices = new List<PriceTick>
        {
            new() { Date = new DateTime(2024, 1, 1), Close = 100.0 },
            new() { Date = new DateTime(2024, 1, 15), Close = 110.0 },
            new() { Date = new DateTime(2024, 1, 31), Close = 120.0 }
        };

        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((prices, (string?)null));

        // Act
        var result = await _service.CalculateMomentumAsync("AAPL", new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));

        // Assert
        Assert.Null(result.Error);
        Assert.True(result.IsBuyCandidate);
        Assert.Equal(20.0, result.Momentum);
        Assert.Equal(100.0, result.StartPrice);
        Assert.Equal(120.0, result.EndPrice);
        Assert.Equal(20.0, result.PercentChange);
    }

    [Fact]
    public async Task CalculateMomentumAsync_WithNegativeMomentum_ReturnsNotBuyCandidate()
    {
        // Arrange
        var prices = new List<PriceTick>
        {
            new() { Date = new DateTime(2024, 1, 1), Close = 100.0 },
            new() { Date = new DateTime(2024, 1, 31), Close = 80.0 }
        };

        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((prices, (string?)null));

        // Act
        var result = await _service.CalculateMomentumAsync("AAPL", new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));

        // Assert
        Assert.Null(result.Error);
        Assert.False(result.IsBuyCandidate);
        Assert.Equal(-20.0, result.Momentum);
        Assert.Equal(-20.0, result.PercentChange);
    }

    [Fact]
    public async Task CalculateMomentumAsync_WithError_ReturnsError()
    {
        // Arrange
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(((List<PriceTick>?)null, "Ticker not found"));

        // Act
        var result = await _service.CalculateMomentumAsync("INVALID", new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));

        // Assert
        Assert.NotNull(result.Error);
        Assert.Equal("Ticker not found", result.Error);
        Assert.False(result.IsBuyCandidate);
    }

    [Fact]
    public async Task CalculateMomentumAsync_WithInsufficientData_ReturnsError()
    {
        // Arrange
        var prices = new List<PriceTick>
        {
            new() { Date = new DateTime(2024, 1, 1), Close = 100.0 }
        };

        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((prices, (string?)null));

        // Act
        var result = await _service.CalculateMomentumAsync("AAPL", new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));

        // Assert
        Assert.NotNull(result.Error);
        Assert.Contains("Insufficient", result.Error);
    }

    [Fact]
    public async Task CalculateMomentumAsync_TrimsAndUppercasesTicker()
    {
        // Arrange
        var prices = new List<PriceTick>
        {
            new() { Date = new DateTime(2024, 1, 1), Close = 100.0 },
            new() { Date = new DateTime(2024, 1, 31), Close = 110.0 }
        };

        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync("AAPL", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((prices, (string?)null));

        // Act
        var result = await _service.CalculateMomentumAsync("  aapl  ", new DateTime(2024, 1, 1), new DateTime(2024, 1, 31));

        // Assert
        Assert.Equal("AAPL", result.Ticker);
    }

    [Fact]
    public async Task CalculateBatchMomentumAsync_ReturnsBestBuyAndBestSell()
    {
        // Arrange
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync("AAPL", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((new List<PriceTick>
            {
                new() { Date = new DateTime(2024, 1, 1), Close = 100.0 },
                new() { Date = new DateTime(2024, 1, 31), Close = 120.0 }
            }, (string?)null));

        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync("MSFT", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((new List<PriceTick>
            {
                new() { Date = new DateTime(2024, 1, 1), Close = 100.0 },
                new() { Date = new DateTime(2024, 1, 31), Close = 90.0 }
            }, (string?)null));

        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync("GOOGL", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((new List<PriceTick>
            {
                new() { Date = new DateTime(2024, 1, 1), Close = 100.0 },
                new() { Date = new DateTime(2024, 1, 31), Close = 105.0 }
            }, (string?)null));

        // Act
        var result = await _service.CalculateBatchMomentumAsync(
            new List<string> { "AAPL", "MSFT", "GOOGL" },
            new DateTime(2024, 1, 1),
            new DateTime(2024, 1, 31));

        // Assert
        Assert.Equal(3, result.Results.Count);
        Assert.Equal("AAPL", result.BestBuy);  // Highest momentum (+20)
        Assert.Equal("MSFT", result.BestSell); // Lowest momentum (-10)
    }

    [Fact]
    public async Task CalculateBatchMomentumAsync_WithAllErrors_ReturnsNullBestBuyAndBestSell()
    {
        // Arrange
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(((List<PriceTick>?)null, "Error"));

        // Act
        var result = await _service.CalculateBatchMomentumAsync(
            new List<string> { "INVALID1", "INVALID2" },
            new DateTime(2024, 1, 1),
            new DateTime(2024, 1, 31));

        // Assert
        Assert.Null(result.BestBuy);
        Assert.Null(result.BestSell);
        Assert.All(result.Results, r => Assert.NotNull(r.Error));
    }
}
