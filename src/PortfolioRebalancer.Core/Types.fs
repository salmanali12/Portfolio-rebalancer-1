module PortfolioRebalancer.Core.Types

// Enhanced error types with better categorization
type ScrapingError = 
    | NavigationError of string
    | ElementNotFound of string
    | DataExtractionError of string
    | PortfolioCreationError of string
    | FileOperationError of string
    | TimeoutError of string
    | NetworkError of string

type ValidationError =
    | InvalidCashAmount of decimal
    | InvalidSymbol of string
    | MissingPriceData of string list
    | EmptyIndex
    | EmptyPortfolio
    | InsufficientData of string
    | InvalidWeight of decimal
    | InvalidPrice of decimal
    | InvalidShares of int

// Enhanced result types for better error handling
type ValidationResult<'T> = Result<'T, ValidationError>

[<CLIMutable>]
type IndexTableRecord = {
    Symbol: string
    Weight: decimal
}

[<CLIMutable>]
type StockRecord = {
    Symbol: string
    Name: string
    Price: decimal 
}

type CurrentPortfolio = {
    Symbol: string
    Shares: int
    Price: decimal 
}

// New type for index information from PSX
type IndexInfo = {
    Name: string
    Code: string
    Current: decimal
    Change: decimal
    ChangePercent: decimal
    High: decimal
    Low: decimal
}

// New type for index selection
type IndexSelection = 
    | KSE30
    | KSE100
    | Both

// Type for scraped index data
type ScrapedIndexData = {
    Symbol: string
    Weight: decimal
    Price: decimal
    Name: string
}

// New types for better structure
type RebalanceOrder = {
    Symbol: string
    Shares: int
    Price: decimal
    Value: decimal
    Commission: decimal
}

type FinalPortfolioPosition = {
    Symbol: string
    Shares: int
    Value: decimal
    Commission: decimal
    Price: decimal
    Weight: decimal
    FinalWeight: decimal
}

type RebalanceResult = {
    Sells: RebalanceOrder list
    Buys: RebalanceOrder list
    Final: FinalPortfolioPosition list
    CashAfterSelling: decimal
    CashAfterBuying: decimal
    TotalSellValue: decimal
    TotalBuyValue: decimal
    TotalSellCommission: decimal
    TotalBuyCommission: decimal
}

type IndexRebalanceResult = {
    Orders: RebalanceOrder list
    TotalValue: decimal
    TotalCommission: decimal
    RemainingCash: decimal
}

type RebalanceConfig = {
    IncludeCommission: bool
    Cash: decimal
    Index: IndexTableRecord list
    Current: CurrentPortfolio list
    Prices: StockRecord list
}

// Validation types
type ValidatedRebalanceConfig = {
    IncludeCommission: bool
    Cash: decimal
    Index: IndexTableRecord list
    Current: CurrentPortfolio list
    Prices: StockRecord list
    MissingSymbols: string list
}

// Domain types for better semantics
type Symbol = Symbol of string
type CashAmount = CashAmount of decimal
type ShareCount = ShareCount of int
type Price = Price of decimal
type Weight = Weight of decimal
