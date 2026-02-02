using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TSMOM.Components.Pages;
using TSMOM.Core.Models;
using TSMOM.Core.Services;

namespace TSMOM.Maui.Tests;

public class AnalyzePageTests : TestContext
{
    private readonly Mock<IStockDataProvider> _mockProvider;
    private readonly MomentumService _momentumService;

    public AnalyzePageTests()
    {
        _mockProvider = new Mock<IStockDataProvider>();
        _momentumService = new MomentumService(_mockProvider.Object);
        Services.AddSingleton(_momentumService);
    }

    #region Step 1: Date Range Tests

    [Fact]
    public void InitialState_ShowsStep1WithEnabledDateInputs()
    {
        // Arrange & Act
        var cut = RenderComponent<Analyze>();

        // Assert
        var startDateInput = cut.Find("#startDate");
        var endDateInput = cut.Find("#endDate");

        Assert.False(startDateInput.HasAttribute("disabled"));
        Assert.False(endDateInput.HasAttribute("disabled"));
        Assert.DoesNotContain("Edit Dates", cut.Markup);
    }

    [Fact]
    public void Step1_DateInputsHaveDefaultValues()
    {
        // Arrange & Act
        var cut = RenderComponent<Analyze>();

        // Assert - dates should have values (3 months range)
        var startDateInput = cut.Find("#startDate");
        var endDateInput = cut.Find("#endDate");

        Assert.NotNull(startDateInput.GetAttribute("value"));
        Assert.NotNull(endDateInput.GetAttribute("value"));
    }

    [Fact]
    public void Step1_EditDatesButton_NotVisibleInitially()
    {
        // Arrange & Act
        var cut = RenderComponent<Analyze>();

        // Assert
        Assert.DoesNotContain("Edit Dates", cut.Markup);
    }

    #endregion

    #region Step 2: Find Best Buy Tests

    [Fact]
    public void Step2_FindBestPerformerButton_DisabledWhenInputEmpty()
    {
        // Arrange & Act
        var cut = RenderComponent<Analyze>();

        // Assert
        var button = cut.Find("button.btn-primary");
        Assert.True(button.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Step2_FindBestPerformerButton_EnabledWhenInputHasValue()
    {
        // Arrange
        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");

        // Act
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });

        // Assert
        var button = cut.Find("button.btn-primary");
        Assert.False(button.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Step2_AllTickersFailed_ShowsDangerAlertWithRetryAll()
    {
        // Arrange
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(((List<PriceTick>?)null, "Ticker not found"));

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "INVALID1, INVALID2" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("alert-danger", cut.Markup);
        Assert.Contains("No valid stock data found", cut.Markup);
        Assert.Contains("Retry All", cut.Markup);
    }

    [Fact]
    public async Task Step2_NoPositiveMomentum_ShowsWarningAlert()
    {
        // Arrange - Both stocks have negative momentum
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync("AAPL", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((new List<PriceTick>
            {
                new() { Date = DateTime.Now.AddMonths(-3), Close = 100.0 },
                new() { Date = DateTime.Now, Close = 80.0 }  // -20%
            }, (string?)null));
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync("MSFT", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((new List<PriceTick>
            {
                new() { Date = DateTime.Now.AddMonths(-3), Close = 100.0 },
                new() { Date = DateTime.Now, Close = 90.0 }  // -10%
            }, (string?)null));

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL, MSFT" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert - No best buy found due to all negative momentum
        Assert.Contains("alert-warning", cut.Markup);
        Assert.Contains("No stocks with positive momentum found", cut.Markup);
        Assert.DoesNotContain("Step 3", cut.Markup);  // Should not advance to step 3
    }

    [Fact]
    public async Task Step2_BestBuyFound_ShowsSuccessAlertAndAdvancesToStep3()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithPositiveMomentum("MSFT", 100.0, 110.0);

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL, MSFT" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("alert-success", cut.Markup);
        Assert.Contains("Best Performer: AAPL", cut.Markup);
        Assert.Contains("Step 3", cut.Markup);
    }

    [Fact]
    public async Task Step2_BestBuyFound_ShowsResultsTableWithBestBuyBadge()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithPositiveMomentum("MSFT", 100.0, 110.0);

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL, MSFT" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("table-success", cut.Markup); // Best buy row highlighting
        Assert.Contains("Best Buy", cut.Markup);      // Badge text
    }

    [Fact]
    public async Task Step2_MixedResults_ShowsRetryFailedButton()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync("INVALID", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(((List<PriceTick>?)null, "Ticker not found"));

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL, INVALID" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("Retry Failed", cut.Markup);
        Assert.Contains("table-danger", cut.Markup); // Error row highlighting
    }

    [Fact]
    public async Task Step2_ResultsTableSortedByPercentChangeDescending()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 130.0);  // +30%
        SetupMockWithPositiveMomentum("MSFT", 100.0, 120.0);  // +20%
        SetupMockWithPositiveMomentum("GOOGL", 100.0, 110.0); // +10%

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL, MSFT, GOOGL" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert - AAPL should appear first (highest % change)
        var tableBody = cut.Find("tbody");
        var rows = tableBody.QuerySelectorAll("tr");
        Assert.Contains("AAPL", rows[0].InnerHtml);
    }

    [Fact]
    public async Task Step2_TextareaDisabled_AfterAdvancingToStep3()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        textarea = cut.Find("#buyCandidates");
        Assert.True(textarea.HasAttribute("disabled"));
    }

    [Fact]
    public async Task Step2_EditButton_AppearsAfterAdvancingToStep3()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });

        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("Edit", cut.Markup);
    }

    #endregion

    #region Step 3: Portfolio Analysis Tests

    [Fact]
    public void Step3_NotVisible_Initially()
    {
        // Arrange & Act
        var cut = RenderComponent<Analyze>();

        // Assert
        Assert.DoesNotContain("Step 3: Analyze Portfolio", cut.Markup);
    }

    [Fact]
    public async Task Step3_Visible_AfterBestBuyFound()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("Step 3: Analyze Portfolio for Sell Candidate", cut.Markup);
    }

    [Fact]
    public async Task Step3_AnalyzeButton_ShowsResults()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithNegativeMomentum("MSFT", 100.0, 90.0);
        SetupMockWithPositiveMomentum("GOOGL", 100.0, 105.0);

        var cut = RenderComponent<Analyze>();

        // Step 2: Find best buy
        var buyCandidatesTextarea = cut.Find("#buyCandidates");
        await buyCandidatesTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        var findButton = cut.Find("button.btn-primary");
        await findButton.ClickAsync(new MouseEventArgs());

        // Step 3: Analyze portfolio
        var portfolioTextarea = cut.Find("#tickers");
        await portfolioTextarea.ChangeAsync(new ChangeEventArgs { Value = "MSFT, GOOGL" });
        var analyzeButtons = cut.FindAll("button.btn-primary");
        var analyzeButton = analyzeButtons.Last(); // Get the Step 3 Analyze button
        await analyzeButton.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("Stock to Buy:", cut.Markup);
        Assert.Contains("AAPL", cut.Markup);
        Assert.Contains("Stock to Sell:", cut.Markup);
    }

    [Fact]
    public async Task Step3_EmptyInput_ShowsErrorMessage()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);

        var cut = RenderComponent<Analyze>();
        var buyCandidatesTextarea = cut.Find("#buyCandidates");
        await buyCandidatesTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        var findButton = cut.Find("button.btn-primary");
        await findButton.ClickAsync(new MouseEventArgs());

        // Step 3: Try to analyze with empty input (clear any default)
        var portfolioTextarea = cut.Find("#tickers");
        await portfolioTextarea.ChangeAsync(new ChangeEventArgs { Value = "   " });
        var analyzeButtons = cut.FindAll("button.btn-primary");
        var analyzeButton = analyzeButtons.Last();
        await analyzeButton.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("Please enter at least one ticker symbol", cut.Markup);
    }

    #endregion

    #region Results Section Tests

    [Fact]
    public async Task Results_ShowsBuyAndSellRecommendations()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithNegativeMomentum("MSFT", 100.0, 80.0);  // Lowest momentum - sell candidate
        SetupMockWithPositiveMomentum("GOOGL", 100.0, 110.0);

        var cut = RenderComponent<Analyze>();

        // Complete workflow
        var buyCandidatesTextarea = cut.Find("#buyCandidates");
        await buyCandidatesTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        var findButton = cut.Find("button.btn-primary");
        await findButton.ClickAsync(new MouseEventArgs());

        var portfolioTextarea = cut.Find("#tickers");
        await portfolioTextarea.ChangeAsync(new ChangeEventArgs { Value = "MSFT, GOOGL" });
        var analyzeButtons = cut.FindAll("button.btn-primary");
        await analyzeButtons.Last().ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("Stock to Buy:", cut.Markup);
        Assert.Contains("AAPL", cut.Markup);
        Assert.Contains("Stock to Sell:", cut.Markup);
        Assert.Contains("MSFT", cut.Markup);
    }

    [Fact]
    public async Task Results_PortfolioTable_ShowsSellCandidateBadge()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithNegativeMomentum("MSFT", 100.0, 80.0);

        var cut = RenderComponent<Analyze>();

        // Complete workflow
        var buyCandidatesTextarea = cut.Find("#buyCandidates");
        await buyCandidatesTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        var portfolioTextarea = cut.Find("#tickers");
        await portfolioTextarea.ChangeAsync(new ChangeEventArgs { Value = "MSFT" });
        await cut.FindAll("button.btn-primary").Last().ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("Sell Candidate", cut.Markup);
        Assert.Contains("table-warning", cut.Markup);
    }

    [Fact]
    public async Task Results_PortfolioTable_ShowsHoldBadgeForNonSellStocks()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithNegativeMomentum("MSFT", 100.0, 80.0);
        SetupMockWithPositiveMomentum("GOOGL", 100.0, 110.0);

        var cut = RenderComponent<Analyze>();

        // Complete workflow
        var buyCandidatesTextarea = cut.Find("#buyCandidates");
        await buyCandidatesTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        var portfolioTextarea = cut.Find("#tickers");
        await portfolioTextarea.ChangeAsync(new ChangeEventArgs { Value = "MSFT, GOOGL" });
        await cut.FindAll("button.btn-primary").Last().ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("Hold", cut.Markup);
    }

    [Fact]
    public async Task Results_StartOverButton_ResetsToStep1()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithPositiveMomentum("MSFT", 100.0, 110.0);

        var cut = RenderComponent<Analyze>();

        // Complete workflow
        var buyCandidatesTextarea = cut.Find("#buyCandidates");
        await buyCandidatesTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        var portfolioTextarea = cut.Find("#tickers");
        await portfolioTextarea.ChangeAsync(new ChangeEventArgs { Value = "MSFT" });
        await cut.FindAll("button.btn-primary").Last().ClickAsync(new MouseEventArgs());

        // Act - Click Start Over
        var startOverButton = cut.Find("button.btn-outline-secondary");
        await startOverButton.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.DoesNotContain("Stock to Buy:", cut.Markup);
        Assert.DoesNotContain("Step 3", cut.Markup);
        var dateInput = cut.Find("#startDate");
        Assert.False(dateInput.HasAttribute("disabled"));
    }

    #endregion

    #region Navigation Tests

    [Fact]
    public async Task EditDates_ResetsToStep1AndClearsResults()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);

        var cut = RenderComponent<Analyze>();
        var buyCandidatesTextarea = cut.Find("#buyCandidates");
        await buyCandidatesTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        // Verify we're at Step 3
        Assert.Contains("Step 3", cut.Markup);

        // Act - Click Edit Dates
        var editDatesButton = cut.FindAll("button.btn-outline-secondary").First();
        await editDatesButton.ClickAsync(new MouseEventArgs());

        // Assert
        var dateInput = cut.Find("#startDate");
        Assert.False(dateInput.HasAttribute("disabled"));
        Assert.DoesNotContain("Step 3", cut.Markup);
        Assert.DoesNotContain("Best Performer: AAPL", cut.Markup);  // The success alert is cleared
    }

    [Fact]
    public async Task EditBuyCandidates_ResetsToStep2AndClearsResults()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);

        var cut = RenderComponent<Analyze>();
        var buyCandidatesTextarea = cut.Find("#buyCandidates");
        await buyCandidatesTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        // Verify we're at Step 3
        Assert.Contains("Step 3", cut.Markup);

        // Act - Click Edit (for buy candidates)
        var editButton = cut.FindAll("button.btn-outline-secondary")
            .First(b => b.TextContent.Contains("Edit") && !b.TextContent.Contains("Dates"));
        await editButton.ClickAsync(new MouseEventArgs());

        // Assert
        var textarea = cut.Find("#buyCandidates");
        Assert.False(textarea.HasAttribute("disabled"));
        Assert.DoesNotContain("Step 3", cut.Markup);
    }

    #endregion

    #region Loading State Tests

    [Fact]
    public async Task Loading_DisablesInputsAndShowsSpinner()
    {
        // Arrange
        var tcs = new TaskCompletionSource<(List<PriceTick>?, string?)>();
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync("AAPL", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(tcs.Task);

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });

        // Act - Start loading
        var button = cut.Find("button.btn-primary");
        var clickTask = button.ClickAsync(new MouseEventArgs());

        // Assert - During loading
        Assert.Contains("spinner-border", cut.Markup);

        // Cleanup
        tcs.SetResult((new List<PriceTick>
        {
            new() { Date = DateTime.Now.AddMonths(-3), Close = 100.0 },
            new() { Date = DateTime.Now, Close = 120.0 }
        }, null));
        await clickTask;
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExceptionDuringAnalysis_ShowsErrorMessage()
    {
        // Arrange
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new Exception("Network error"));

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("An error occurred", cut.Markup);
        Assert.Contains("Network error", cut.Markup);
    }

    [Fact]
    public async Task PerTickerError_ShowsInlineInTable()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync("INVALID", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(((List<PriceTick>?)null, "Ticker not found"));

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL, INVALID" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("Ticker not found", cut.Markup);
        Assert.Contains("text-danger", cut.Markup);
    }

    #endregion

    #region Ticker Parsing Tests

    [Fact]
    public async Task TickerParsing_HandlesCommasSeparation()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithPositiveMomentum("MSFT", 100.0, 110.0);

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL,MSFT" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("AAPL", cut.Markup);
        Assert.Contains("MSFT", cut.Markup);
    }

    [Fact]
    public async Task TickerParsing_HandlesNewlineSeparation()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithPositiveMomentum("MSFT", 100.0, 110.0);

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL\nMSFT" });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("AAPL", cut.Markup);
        Assert.Contains("MSFT", cut.Markup);
    }

    [Fact]
    public async Task TickerParsing_TrimsWhitespaceAndUppercases()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "  aapl  " });

        // Act
        var button = cut.Find("button.btn-primary");
        await button.ClickAsync(new MouseEventArgs());

        // Assert
        _mockProvider.Verify(
            p => p.GetPriceHistoryAsync("AAPL", It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    #endregion

    #region Retry Logic Tests

    [Fact]
    public async Task RetryFailed_RetriesOnlyFailedTickers()
    {
        // Arrange - First call: AAPL succeeds, INVALID fails
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        var invalidCallCount = 0;
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync("INVALID", It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(() =>
            {
                invalidCallCount++;
                if (invalidCallCount == 1)
                    return ((List<PriceTick>?)null, "Ticker not found");
                // On retry, succeed
                return (new List<PriceTick>
                {
                    new() { Date = DateTime.Now.AddMonths(-3), Close = 100.0 },
                    new() { Date = DateTime.Now, Close = 115.0 }
                }, null);
            });

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL, INVALID" });
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        // Verify retry button exists
        Assert.Contains("Retry Failed", cut.Markup);

        // Act - Click retry
        var retryButton = cut.FindAll("button").First(b => b.TextContent.Contains("Retry Failed"));
        await retryButton.ClickAsync(new MouseEventArgs());

        // Assert - INVALID was called twice (initial + retry), AAPL only once
        _mockProvider.Verify(
            p => p.GetPriceHistoryAsync("INVALID", It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Exactly(2));
        _mockProvider.Verify(
            p => p.GetPriceHistoryAsync("AAPL", It.IsAny<DateTime>(), It.IsAny<DateTime>()),
            Times.Once);
    }

    #endregion

    #region Buy Ticker Exclusion Tests

    [Fact]
    public async Task Step3_BuyTickerExcludedFromPortfolioAnalysis()
    {
        // Arrange - AAPL is the buy ticker
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithPositiveMomentum("MSFT", 100.0, 110.0);
        SetupMockWithNegativeMomentum("GOOGL", 100.0, 90.0);

        var cut = RenderComponent<Analyze>();

        // Step 2: Select AAPL as buy ticker
        var buyCandidatesTextarea = cut.Find("#buyCandidates");
        await buyCandidatesTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        // Step 3: Include AAPL in portfolio (should be filtered out)
        var portfolioTextarea = cut.Find("#tickers");
        await portfolioTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL, MSFT, GOOGL" });
        await cut.FindAll("button.btn-primary").Last().ClickAsync(new MouseEventArgs());

        // Assert - Portfolio Analysis section should exist
        Assert.Contains("Portfolio Analysis", cut.Markup);

        // Find the portfolio table (the one after "Portfolio Analysis" heading, without table-sm class)
        var allTables = cut.FindAll("table");
        var portfolioTable = allTables.Last(); // Portfolio table is the last one
        var tableHtml = portfolioTable.InnerHtml;

        // MSFT and GOOGL should be in the results
        Assert.Contains("MSFT", tableHtml);
        Assert.Contains("GOOGL", tableHtml);

        // AAPL should NOT be in the portfolio table (it's the buy ticker)
        var aaplInTable = portfolioTable.QuerySelectorAll("td").Count(td => td.TextContent.Contains("AAPL"));
        Assert.Equal(0, aaplInTable);
    }

    [Fact]
    public async Task Step3_BuyTickerExcluded_ShowsErrorIfOnlyBuyTickerEntered()
    {
        // Arrange - AAPL is the buy ticker
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);

        var cut = RenderComponent<Analyze>();

        // Step 2: Select AAPL as buy ticker
        var buyCandidatesTextarea = cut.Find("#buyCandidates");
        await buyCandidatesTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        // Step 3: Enter only AAPL (which will be filtered out, leaving empty list)
        var portfolioTextarea = cut.Find("#tickers");
        await portfolioTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        await cut.FindAll("button.btn-primary").Last().ClickAsync(new MouseEventArgs());

        // Assert - Should show error since no valid tickers remain
        Assert.Contains("Please enter at least one ticker symbol", cut.Markup);
    }

    [Fact]
    public async Task Step3_BuyTickerExcluded_CaseInsensitive()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithPositiveMomentum("MSFT", 100.0, 110.0);

        var cut = RenderComponent<Analyze>();

        // Step 2: Select AAPL as buy ticker
        var buyCandidatesTextarea = cut.Find("#buyCandidates");
        await buyCandidatesTextarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        // Step 3: Enter aapl in lowercase (should still be filtered out)
        var portfolioTextarea = cut.Find("#tickers");
        await portfolioTextarea.ChangeAsync(new ChangeEventArgs { Value = "aapl, MSFT" });
        await cut.FindAll("button.btn-primary").Last().ClickAsync(new MouseEventArgs());

        // Assert - Portfolio Analysis section should exist
        Assert.Contains("Portfolio Analysis", cut.Markup);

        // Find the portfolio table (the last table on the page)
        var allTables = cut.FindAll("table");
        var portfolioTable = allTables.Last();

        // Only MSFT should be in portfolio results
        Assert.Contains("MSFT", portfolioTable.InnerHtml);

        // AAPL should NOT be in the portfolio table
        var aaplInTable = portfolioTable.QuerySelectorAll("td").Count(td => td.TextContent.Contains("AAPL"));
        Assert.Equal(0, aaplInTable);
    }

    #endregion

    #region Momentum Display Tests

    [Fact]
    public async Task MomentumDisplay_PositiveValues_ShowGreenWithPlusSign()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL" });

        // Act
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("text-success", cut.Markup);
        Assert.Contains("+20.00", cut.Markup);
    }

    [Fact]
    public async Task MomentumDisplay_NegativeValues_ShowRedWithMinusSign()
    {
        // Arrange
        SetupMockWithPositiveMomentum("AAPL", 100.0, 120.0);
        SetupMockWithNegativeMomentum("MSFT", 100.0, 80.0);

        var cut = RenderComponent<Analyze>();
        var textarea = cut.Find("#buyCandidates");
        await textarea.ChangeAsync(new ChangeEventArgs { Value = "AAPL, MSFT" });

        // Act
        await cut.Find("button.btn-primary").ClickAsync(new MouseEventArgs());

        // Assert
        Assert.Contains("text-danger", cut.Markup);
        Assert.Contains("-20.00", cut.Markup);
    }

    #endregion

    #region Helper Methods

    private void SetupMockWithPositiveMomentum(string ticker, double startPrice, double endPrice)
    {
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync(ticker, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((new List<PriceTick>
            {
                new() { Date = DateTime.Now.AddMonths(-3), Close = startPrice },
                new() { Date = DateTime.Now, Close = endPrice }
            }, (string?)null));
    }

    private void SetupMockWithNegativeMomentum(string ticker, double startPrice, double endPrice)
    {
        _mockProvider
            .Setup(p => p.GetPriceHistoryAsync(ticker, It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync((new List<PriceTick>
            {
                new() { Date = DateTime.Now.AddMonths(-3), Close = startPrice },
                new() { Date = DateTime.Now, Close = endPrice }
            }, (string?)null));
    }

    #endregion
}
