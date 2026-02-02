# UI Workflow Specification

This document describes the user interface workflow for the TSMOM Momentum Analysis feature.

## Overview

The analysis workflow is a 3-step wizard that guides users through:
1. Selecting a date range for analysis
2. Finding the best stock to buy from candidates
3. Analyzing their portfolio to identify a sell candidate

## Page: Analyze (`/` and `/analyze`)

### State Variables

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `currentStep` | int | 1 | Current workflow step (1-3) |
| `isLoading` | bool | false | Loading spinner state |
| `startDate` | DateTime | Now - 3 months | Analysis period start |
| `endDate` | DateTime | Now | Analysis period end |
| `buyCandidatesInput` | string | "" | Raw ticker input for buy candidates |
| `tickerInput` | string | "" | Raw ticker input for portfolio |
| `buyCandidatesResult` | BatchMomentumResponse? | null | Results from Step 2 |
| `portfolioResult` | BatchMomentumResponse? | null | Results from Step 3 |
| `confirmedBuyTicker` | string? | null | Selected stock to buy |
| `errorMessage` | string? | null | Global error display |

---

## Step 1: Select Date Range

### UI Elements
- **Start Date** input (date picker)
- **End Date** input (date picker)
- **Edit Dates** button (visible only when `currentStep > 1`)

### Behavior

| Condition | Behavior |
|-----------|----------|
| `currentStep == 1` | Date inputs are enabled |
| `currentStep > 1` | Date inputs are disabled; "Edit Dates" button appears |
| User clicks "Edit Dates" | Resets to Step 1, clears all results |

### Edit Dates Action
Triggers `EditDates()`:
- Sets `currentStep = 1`
- Clears `buyCandidatesResult`, `confirmedBuyTicker`, `portfolioResult`

---

## Step 2: Find Best Stock to Buy

### UI Elements
- **Candidate Tickers** textarea (comma or newline separated)
- **Find Best Performer** button
- **Edit** button (visible only when `currentStep > 2`)
- **Retry Failed** / **Retry All** button (conditional)
- Results table (conditional)

### Input Parsing
Tickers are parsed from input:
```
Split by: comma, newline, carriage return
Transform: trim whitespace, convert to uppercase
Filter: remove empty strings
```

### Button States

| Button | Enabled When |
|--------|--------------|
| Find Best Performer | `buyCandidatesInput` is not empty AND `!isLoading` AND `currentStep <= 2` |
| Edit | `currentStep > 2` |
| Retry Failed | `isLoading == false` AND there are failed tickers |

### Textarea State
- **Disabled** when `currentStep > 2` OR `isLoading == true`

### Analysis Flow (`FindBestBuyAsync`)

```
1. Set isLoading = true, currentStep = 2
2. Parse tickers from input
3. If no tickers → show error "Please enter at least one ticker symbol"
4. Call MomentumService.CalculateBatchMomentumAsync()
5. Store result in buyCandidatesResult
6. If BestBuy found:
   - Set confirmedBuyTicker = BestBuy
   - Advance to currentStep = 3
7. Set isLoading = false
```

### Results Display

#### Scenario A: All Tickers Failed
- Show danger alert with list of all ticker errors
- Show "Retry All" button

#### Scenario B: Some Succeeded, No Positive Momentum
- Show warning alert: "No stocks with positive momentum found"
- User cannot proceed to Step 3

#### Scenario C: Best Buy Found
- Show success alert with best performer details:
  - Ticker name
  - Momentum value (with +/- sign)
  - Percent change
  - Price range (start → end)
- Show results table sorted by % change (descending)
- If some tickers failed, show "Retry Failed (N)" button

### Results Table Columns
| Column | Description |
|--------|-------------|
| Ticker | Stock symbol (bold) |
| Momentum | Momentum value, green if positive, red if negative |
| % Change | Percentage change, green if positive, red if negative |
| Status | "Best Buy" badge for winner |

### Row Styling
| Condition | Row Class |
|-----------|-----------|
| Is best buy | `table-success` (green) |
| Has error | `table-danger` (red) |
| Normal | Default |

### Retry Logic (`RetryFailedBuyCandidatesAsync`)
1. Get list of failed tickers from current results
2. Re-run momentum calculation for failed tickers only
3. Merge successful retries with existing successful results
4. Recalculate best buy from merged results
5. If best buy now exists, advance to Step 3

---

## Step 3: Analyze Portfolio for Sell Candidate

### Visibility
- **Only visible when `currentStep >= 3`**

### UI Elements
- **Portfolio Tickers** textarea
- **Analyze** button

### Button States
| Button | Enabled When |
|--------|--------------|
| Analyze | `!isLoading` |

### Textarea State
- **Disabled** when `isLoading == true`

### Analysis Flow (`AnalyzePortfolioAsync`)
```
1. Set isLoading = true
2. Parse tickers from input
3. Filter out confirmedBuyTicker (buy ticker cannot be in sell list)
4. If no tickers remain → show error "Please enter at least one ticker symbol"
5. Call MomentumService.CalculateBatchMomentumAsync()
6. Store result in portfolioResult
7. Set isLoading = false
```

### Validation Rules
- The confirmed buy ticker is automatically excluded from portfolio analysis
- This prevents the same stock from being both the buy and sell recommendation
- Exclusion is case-insensitive (input is normalized to uppercase)

---

## Results Section

### Visibility
- **Only visible when `portfolioResult != null`**

### Layout
Two-column summary at top:
- Left: "Stock to Buy: {confirmedBuyTicker}" (success alert)
- Right: "Stock to Sell: {BestSell}" (warning alert)

### Portfolio Table Columns
| Column | Description |
|--------|-------------|
| Ticker | Stock symbol (bold) |
| Start Price | Price at start date |
| End Price | Price at end date |
| Momentum | Momentum value with color |
| % Change | Percentage change with color |
| Status | "Sell Candidate" or "Hold" badge |

### Row Styling
| Condition | Row Class |
|-----------|-----------|
| Is sell candidate | `table-warning` (yellow) |
| Has error | Error message spans columns |
| Normal | Default |

### Status Badges
| Badge | Condition |
|-------|-----------|
| `bg-warning text-dark` "Sell Candidate" | Lowest momentum stock |
| `bg-secondary` "Hold" | All other stocks |

### Start Over Button
Triggers `Reset()`:
- Sets `currentStep = 1`
- Clears all inputs and results

---

## Error Handling

### Global Error Display
- Shows when `errorMessage != null`
- Displayed as danger alert below Step 3 card
- Cleared at start of each async operation

### Per-Ticker Errors
- Displayed inline in results tables
- Error message spans remaining columns
- Row highlighted in red (`table-danger` or `text-danger`)

### Exception Handling
All async operations wrap in try/catch:
- Catches any `Exception`
- Sets `errorMessage` to exception message
- `finally` block ensures `isLoading = false`

---

## State Transitions

```
┌─────────────────────────────────────────────────────────────┐
│                      Step 1: Date Range                      │
│                    (dates editable)                          │
└─────────────────────────┬───────────────────────────────────┘
                          │ User clicks "Find Best Performer"
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                  Step 2: Buy Candidates                      │
│              (dates locked, tickers editable)                │
└─────────────────────────┬───────────────────────────────────┘
                          │ Best buy found (positive momentum)
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                  Step 3: Portfolio Analysis                  │
│         (dates locked, buy locked, portfolio editable)       │
└─────────────────────────┬───────────────────────────────────┘
                          │ User clicks "Analyze"
                          ▼
┌─────────────────────────────────────────────────────────────┐
│                        Results                               │
│                  (all inputs locked)                         │
└─────────────────────────────────────────────────────────────┘

Backward Navigation:
- "Edit Dates" → Resets to Step 1
- "Edit" (on Step 2) → Resets to Step 2
- "Start Over" → Full reset to Step 1
```

---

## Loading States

During async operations:
1. `isLoading = true`
2. Relevant button shows spinner icon
3. Input fields disabled
4. Other buttons disabled
5. On completion: `isLoading = false`

Spinner displays in button with `spinner-border spinner-border-sm` class.

---

## Validation Rules

### Ticker Input
- Required: At least one non-empty ticker
- Parsing: Splits on comma, newline, CR
- Normalization: Trim + uppercase
- Error: "Please enter at least one ticker symbol"

### Date Range
- No explicit validation in UI
- Service layer handles invalid dates

---

## Momentum Calculation Logic

### Best Buy Selection
- Stock with highest positive momentum from candidates
- Must have `PercentChange > 0`
- If no positive momentum stocks, `BestBuy = null`

### Sell Candidate Selection
- Stock with lowest momentum in portfolio
- Selected from all successfully analyzed stocks
- Displayed with "Sell Candidate" badge
