# TSMOM - Time Series Momentum Analyzer

A cross-platform mobile app for stock momentum analysis built with .NET MAUI Blazor.

## Features

- Analyze stock momentum over a configurable date range
- Validate buy candidates (positive momentum)
- Analyze portfolio to identify sell candidates (lowest momentum)
- Works offline - no backend API required

## Platforms

- Android
- iOS
- Windows
- macOS

## Project Structure

```
TSMOM.Maui/
├── TSMOM.Maui/           # MAUI Blazor app
├── TSMOM.Core/           # Shared business logic
│   ├── Models/           # Data models
│   └── Services/         # Momentum calculations
└── TSMOM.Maui.Tests/     # Unit tests
```

## Getting Started

### Prerequisites

- .NET 8 SDK or later
- MAUI workload: `dotnet workload install maui`
- For Android: Android SDK (via Visual Studio or Android Studio)

### Build and Run

```bash
# Open solution in Visual Studio
start TSMOM.Maui.sln

# Or build from command line
dotnet build TSMOM.Maui/TSMOM.Maui.csproj

# Run tests
dotnet test TSMOM.Maui.Tests
```

### Deploy to Android

1. Open `TSMOM.Maui.sln` in Visual Studio
2. Select Android Emulator or connected device
3. Press F5 to build and deploy

## How It Works

1. Select a date range for momentum analysis
2. Enter a stock ticker to validate as a buy candidate
3. If positive momentum, enter your portfolio tickers
4. App identifies the stock with lowest momentum as sell candidate

## Data Source

Stock data is fetched from Yahoo Finance using [YahooQuotesApi](https://github.com/dshe/YahooQuotesApi).

## License

MIT
