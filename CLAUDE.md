# TSMOM MAUI - Time Series Momentum Analyzer

## Project Overview

Cross-platform mobile app for stock momentum analysis. Self-contained - no backend API required.

## Architecture

- **TSMOM.Core** - Shared business logic (.NET 8 class library)
- **TSMOM.Maui** - MAUI Blazor app (Android/iOS/Windows/Mac)
- **TSMOM.Maui.Tests** - xUnit tests

## Folder Structure

```text
tsmom-maui/
├── TSMOM.Maui/
│   ├── Components/
│   │   ├── Layout/           # NavMenu, MainLayout
│   │   └── Pages/            # Razor pages (Analyze.razor)
│   ├── MauiProgram.cs        # DI registration
│   └── TSMOM.Maui.sln        # Solution file
├── TSMOM.Core/
│   ├── Models/
│   │   └── MomentumModels.cs # DTOs
│   └── Services/
│       ├── IStockDataProvider.cs      # Interface for mocking
│       ├── YahooStockDataProvider.cs  # Yahoo Finance implementation
│       └── MomentumService.cs         # Business logic
├── TSMOM.Maui.Tests/
│   └── MomentumServiceTests.cs
├── CLAUDE.md
└── README.md
```

### Where to Put New Code

- **Business logic** → `TSMOM.Core/Services/`
- **Data models** → `TSMOM.Core/Models/`
- **UI pages** → `TSMOM.Maui/Components/Pages/`
- **Unit tests** → `TSMOM.Maui.Tests/`

## Running the Project

### Visual Studio (Recommended)

1. Open `TSMOM.Maui/TSMOM.Maui.sln`
2. Select target (Android Emulator, Windows Machine, etc.)
3. Press F5

### Command Line

```bash
# Build
dotnet build TSMOM.Maui/TSMOM.Maui.csproj

# Run tests
dotnet test TSMOM.Maui.Tests

# Build for specific platform
dotnet build TSMOM.Maui/TSMOM.Maui.csproj -f net8.0-android
```

## Key Files

- `TSMOM.Core/Services/MomentumService.cs` - Momentum calculations
- `TSMOM.Core/Services/IStockDataProvider.cs` - Stock data interface (for mocking)
- `TSMOM.Maui/Components/Pages/Analyze.razor` - Main UI
- `TSMOM.Maui/MauiProgram.cs` - Dependency injection setup

## Workflow

1. User selects date range
2. User enters stock to buy → validates positive momentum
3. User enters portfolio tickers → finds sell candidate (lowest momentum)

## Dependencies

- .NET 8+ with MAUI workload
- YahooQuotesApi (stock data)
- NodaTime (date handling)
- xUnit + Moq (testing)

## Development Guidelines

### Testing Requirements

- All business logic in `TSMOM.Core` must have tests
- Use `IStockDataProvider` interface to mock Yahoo Finance in tests
- Run tests: `dotnet test TSMOM.Maui.Tests`

### When Adding Features

1. Add business logic to `TSMOM.Core/Services/`
2. Create unit tests in `TSMOM.Maui.Tests/`
3. Update UI in `TSMOM.Maui/Components/Pages/`
4. Verify tests pass before committing

### Pre-Commit Checklist

1. Run tests: `dotnet test TSMOM.Maui.Tests`
2. Build all platforms: `dotnet build TSMOM.Maui/TSMOM.Maui.csproj`
3. No hardcoded API keys or secrets
