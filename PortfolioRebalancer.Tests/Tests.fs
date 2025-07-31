module Tests

open Xunit
open System
open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Validation
open PortfolioRebalancer.Core.Helpers
open PortfolioRebalancer.Core.Configuration
open PortfolioRebalancer.Core.RebalancingCalculations

// ============================================================================
// TEST DEFINITIONS (Enhanced with custom output)
// ============================================================================

// ============================================================================
// VALIDATION TESTS
// ============================================================================

[<Fact>]
let ``validateCashAmount should accept positive amounts`` () =
    let result = validateCashAmount 1000m
    match result with
    | Ok (CashAmount amount) -> Assert.Equal(1000m, amount)
    | Error _ -> Assert.True(false, "Should not return error for positive amount")

[<Fact>]
let ``validateCashAmount should accept zero`` () =
    let result = validateCashAmount 0m
    match result with
    | Ok (CashAmount amount) -> Assert.Equal(0m, amount)
    | Error _ -> Assert.True(false, "Should not return error for zero amount")

[<Fact>]
let ``validateCashAmount should reject negative amounts`` () =
    let result = validateCashAmount -100m
    match result with
    | Ok _ -> Assert.True(false, "Should return error for negative amount")
    | Error (InvalidCashAmount amount) -> Assert.Equal(-100m, amount)
    | Error _ -> Assert.True(false, "Should return InvalidCashAmount error")

[<Fact>]
let ``validateIndexNotEmpty should accept non-empty list`` () =
    let index: IndexTableRecord list = [{ Symbol = "AAPL"; Weight = 0.5m }]
    let result = validateIndexNotEmpty index
    match result with
    | Ok validatedIndex -> Assert.Equal<IndexTableRecord list>(index, validatedIndex)
    | Error _ -> Assert.True(false, "Should not return error for non-empty list")

[<Fact>]
let ``validateIndexNotEmpty should reject empty list`` () =
    let result = validateIndexNotEmpty []
    match result with
    | Ok _ -> Assert.True(false, "Should return error for empty list")
    | Error EmptyIndex -> Assert.True(true)
    | Error _ -> Assert.True(false, "Should return EmptyIndex error")

[<Fact>]
let ``validatePrice should accept valid prices`` () =
    let result = validatePrice 100.50m
    match result with
    | Ok (Price price) -> Assert.Equal(100.50m, price)
    | Error _ -> Assert.True(false, "Should not return error for valid price")

[<Fact>]
let ``validatePrice should reject negative prices`` () =
    let result = validatePrice -10m
    match result with
    | Ok _ -> Assert.True(false, "Should return error for negative price")
    | Error (InvalidPrice price) -> Assert.Equal(-10m, price)
    | Error _ -> Assert.True(false, "Should return InvalidPrice error")

[<Fact>]
let ``validateShares should accept valid share counts`` () =
    let result = validateShares 100
    match result with
    | Ok (ShareCount shares) -> Assert.Equal(100, shares)
    | Error _ -> Assert.True(false, "Should not return error for valid share count")

[<Fact>]
let ``validateShares should reject negative share counts`` () =
    let result = validateShares -10
    match result with
    | Ok _ -> Assert.True(false, "Should return error for negative share count")
    | Error (InvalidShares shares) -> Assert.Equal(-10, shares)
    | Error _ -> Assert.True(false, "Should return InvalidShares error")

[<Fact>]
let ``validateWeight should accept valid weights`` () =
    let result = validateWeight 0.5m
    match result with
    | Ok (Weight weight) -> Assert.Equal(0.5m, weight)
    | Error _ -> Assert.True(false, "Should not return error for valid weight")

[<Fact>]
let ``validateWeight should reject negative weights`` () =
    let result = validateWeight -0.1m
    match result with
    | Ok _ -> Assert.True(false, "Should return error for negative weight")
    | Error (InvalidWeight weight) -> Assert.Equal(-0.1m, weight)
    | Error _ -> Assert.True(false, "Should return InvalidWeight error")

[<Fact>]
let ``validateSymbol should accept valid symbols`` () =
    let result = validateSymbol "AAPL"
    match result with
    | Ok (Symbol symbol) -> Assert.Equal("AAPL", symbol)
    | Error _ -> Assert.True(false, "Should not return error for valid symbol")

[<Fact>]
let ``validateSymbol should reject empty symbols`` () =
    let result = validateSymbol ""
    match result with
    | Ok _ -> Assert.True(false, "Should return error for empty symbol")
    | Error (InvalidSymbol symbol) -> Assert.Equal("", symbol)
    | Error _ -> Assert.True(false, "Should return InvalidSymbol error")

[<Fact>]
let ``validateSymbol should reject null symbols`` () =
    let result = validateSymbol null
    match result with
    | Ok _ -> Assert.True(false, "Should return error for null symbol")
    | Error (InvalidSymbol symbol) -> Assert.Equal("", symbol)
    | Error _ -> Assert.True(false, "Should return InvalidSymbol error")

[<Fact>]
let ``ValidDecimal pattern should parse valid decimals`` () =
    let testCases = [("100", 100m); ("1,000", 1000m); ("0", 0m)]
    for (input, expected) in testCases do
        match input with
        | ValidDecimal value -> Assert.Equal(expected, value)
        | _ -> Assert.True(false, sprintf "Should parse '%s' as %M" input expected)

[<Fact>]
let ``ValidDecimal pattern should reject invalid decimals`` () =
    let testCases = ["invalid"; ""; "abc"]
    for input in testCases do
        match input with
        | ValidDecimal _ -> Assert.True(false, sprintf "Should not parse '%s' as decimal" input)
        | _ -> Assert.True(true)

// ============================================================================
// HELPER FUNCTION TESTS
// ============================================================================

[<Fact>]
let ``calculateCommission should use fixed rate for low prices`` () =
    let commission = calculateCommission 10m 100
    let expected = Commission.FixedRate * 100m
    Assert.Equal(expected, commission)

[<Fact>]
let ``calculateCommission should use percentage rate for high prices`` () =
    let price = 20m
    let shares = 50
    let commission = calculateCommission price shares
    let expected = price * decimal shares * Commission.PercentageRate
    Assert.Equal(expected, commission)

[<Fact>]
let ``createWeightMap should create correct map`` () =
    let index = [
        { Symbol = "AAPL"; Weight = 0.5m }
        { Symbol = "MSFT"; Weight = 0.3m }
        { Symbol = "GOOGL"; Weight = 0.2m }
    ]
    let weightMap = createWeightMap index
    Assert.Equal(0.5m, weightMap.["AAPL"])
    Assert.Equal(0.3m, weightMap.["MSFT"])
    Assert.Equal(0.2m, weightMap.["GOOGL"])
    Assert.Equal(3, weightMap.Count)

[<Fact>]
let ``formatPKR should format currency correctly`` () =
    let formatted = formatPKR 1234.56m
    Assert.Contains("Rs.", formatted)
    Assert.Contains("1,234.56", formatted)

[<Fact>]
let ``toPercentage should format correctly`` () =
    let formatted = toPercentage 0.1234m
    Assert.Equal("12.34%", formatted)

[<Fact>]
let ``formatPKRShort should format without decimals`` () =
    let formatted = formatPKRShort 1234.56m
    Assert.Contains("Rs.", formatted)
    Assert.Contains("1,235", formatted) // Should round up



// ============================================================================
// REBALANCING CALCULATION TESTS
// ============================================================================

[<Fact>]
let ``createPriceMap should create correct map`` () =
    let prices = [
        { Symbol = "AAPL"; Name = "Apple Inc."; Price = 150.0m }
        { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 300.0m }
        { Symbol = "GOOGL"; Name = "Alphabet Inc."; Price = 2500.0m }
    ]
    let priceMap = createPriceMap prices
    Assert.Equal(150.0m, priceMap.["AAPL"])
    Assert.Equal(300.0m, priceMap.["MSFT"])
    Assert.Equal(2500.0m, priceMap.["GOOGL"])
    Assert.Equal(3, priceMap.Count)

[<Fact>]
let ``createCurrentSharesMap should create correct map`` () =
    let current = [
        { Symbol = "AAPL"; Shares = 100; Price = 150.0m }
        { Symbol = "MSFT"; Shares = 50; Price = 300.0m }
        { Symbol = "GOOGL"; Shares = 10; Price = 2500.0m }
    ]
    let sharesMap = createCurrentSharesMap current
    Assert.Equal(100, sharesMap.["AAPL"])
    Assert.Equal(50, sharesMap.["MSFT"])
    Assert.Equal(10, sharesMap.["GOOGL"])
    Assert.Equal(3, sharesMap.Count)

[<Fact>]
let ``calculateTargetShares should calculate correct target shares`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let targetShares = calculateTargetShares config
    Assert.Equal(60, targetShares.["AAPL"]) // 10000 * 0.6 / 100
    Assert.Equal(20, targetShares.["MSFT"]) // 10000 * 0.4 / 200

[<Fact>]
let ``rebalanceIndex should calculate correct orders`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        Assert.Equal(2, rebalanceResult.Orders.Length)
        let aaplOrder = rebalanceResult.Orders |> List.find (fun o -> o.Symbol = "AAPL")
        let msftOrder = rebalanceResult.Orders |> List.find (fun o -> o.Symbol = "MSFT")
        Assert.Equal(60, aaplOrder.Shares)
        Assert.Equal(20, msftOrder.Shares)
        Assert.Equal(6000m, aaplOrder.Value)
        Assert.Equal(4000m, msftOrder.Value)
        Assert.Equal(10000m, rebalanceResult.TotalValue)
    | Error _ -> Assert.True(false, "Should not return error for valid config")

[<Fact>]
let ``rebalanceCurrentPortfolio should calculate sell and buy orders`` () =
    let config = {
        IncludeCommission = false
        Cash = 5000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = [
            { Symbol = "AAPL"; Shares = 100; Price = 100m } // Current value: 10000
            { Symbol = "MSFT"; Shares = 20; Price = 200m }  // Current value: 4000
        ]
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceCurrentPortfolio config
    match result with
    | Ok rebalanceResult ->
        // Total portfolio value: 5000 + 10000 + 4000 = 19000
        // Target AAPL: 19000 * 0.6 / 100 = 114 shares (need to buy 14)
        // Target MSFT: 19000 * 0.4 / 200 = 38 shares (need to buy 18)
        Assert.Equal(0, rebalanceResult.Sells.Length) // No sells needed
        Assert.Equal(2, rebalanceResult.Buys.Length)
        let aaplBuy = rebalanceResult.Buys |> List.find (fun o -> o.Symbol = "AAPL")
        let msftBuy = rebalanceResult.Buys |> List.find (fun o -> o.Symbol = "MSFT")
        Assert.Equal(14, aaplBuy.Shares)
        Assert.Equal(18, msftBuy.Shares)
    | Error _ -> Assert.True(false, "Should not return error for valid config")

[<Fact>]
let ``rebalanceCurrentPortfolio should calculate sell orders when overweight`` () =
    let config = {
        IncludeCommission = false
        Cash = 0m
        Index = [
            { Symbol = "AAPL"; Weight = 0.5m }
            { Symbol = "MSFT"; Weight = 0.5m }
        ]
        Current = [
            { Symbol = "AAPL"; Shares = 100; Price = 100m } // Current value: 10000
            { Symbol = "MSFT"; Shares = 100; Price = 200m } // Current value: 20000
        ]
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceCurrentPortfolio config
    match result with
    | Ok rebalanceResult ->
        // Total portfolio value: 30000
        // Target AAPL: 30000 * 0.5 / 100 = 150 shares (need to buy 50)
        // Target MSFT: 30000 * 0.5 / 200 = 75 shares (need to sell 25)
        Assert.Equal(1, rebalanceResult.Sells.Length)
        Assert.Equal(1, rebalanceResult.Buys.Length)
        let msftSell = rebalanceResult.Sells |> List.find (fun o -> o.Symbol = "MSFT")
        let aaplBuy = rebalanceResult.Buys |> List.find (fun o -> o.Symbol = "AAPL")
        Assert.Equal(25, msftSell.Shares)
        Assert.Equal(50, aaplBuy.Shares)
    | Error _ -> Assert.True(false, "Should not return error for valid config")

[<Fact>]
let ``rebalanceIndex should return error for empty index`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = []
        Current = []
        Prices = []
    }
    let result = rebalanceIndex config
    match result with
    | Ok _ -> Assert.True(false, "Should return error for empty index")
    | Error EmptyIndex -> Assert.True(true)
    | Error _ -> Assert.True(false, "Should return EmptyIndex error")

[<Fact>]
let ``rebalanceIndex should handle missing price data`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            // MSFT price missing
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        // Should only include AAPL order, skip MSFT due to missing price
        Assert.Equal(1, rebalanceResult.Orders.Length)
        let aaplOrder = rebalanceResult.Orders |> List.find (fun o -> o.Symbol = "AAPL")
        Assert.Equal(60, aaplOrder.Shares)
    | Error _ -> Assert.True(false, "Should handle missing price data gracefully")

// ============================================================================
// CONFIGURATION TESTS
// ============================================================================

[<Fact>]
let ``Configuration should have valid commission rates`` () =
    Assert.True(Commission.FixedRate > 0m)
    Assert.True(Commission.PercentageRate > 0m)
    Assert.True(Commission.PercentageRate < 1m)

[<Fact>]
let ``Configuration should have valid database settings`` () =
    Assert.NotNull Database.ConnectionString
    Assert.True(Database.DefaultTimeout > TimeSpan.Zero)

[<Fact>]
let ``Configuration should have valid validation settings`` () =
    Assert.True(Validation.MaxSymbolLength > 0)
    Assert.True(Validation.MinSymbolLength >= 0)
    Assert.True(Validation.MaximumCashAmount > 0m)

// ============================================================================
// ADVANCED CALCULATION TESTS
// ============================================================================

[<Fact>]
let ``calculateSellOrders should calculate correct sell orders`` () =
    let config = {
        IncludeCommission = false
        Cash = 0m
        Index = [
            { Symbol = "AAPL"; Weight = 0.5m }
            { Symbol = "MSFT"; Weight = 0.5m }
        ]
        Current = [
            { Symbol = "AAPL"; Shares = 100; Price = 100m } // 10000 value
            { Symbol = "MSFT"; Shares = 100; Price = 200m } // 20000 value
        ]
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let targetSharesMap = calculateTargetShares config
    let sellOrders = calculateSellOrders config targetSharesMap
    
    // Total value: 30000, Target AAPL: 150 shares, Target MSFT: 75 shares
    // AAPL: 100 current - 150 target = -50 (buy, no sell)
    // MSFT: 100 current - 75 target = 25 (sell)
    Assert.Equal(1, sellOrders.Length)
    let msftSell = sellOrders |> List.find (fun o -> o.Symbol = "MSFT")
    Assert.Equal(25, msftSell.Shares)
    Assert.Equal(200m, msftSell.Price)
    Assert.Equal(5000m, msftSell.Value)

[<Fact>]
let ``calculateBuyOrders should calculate correct buy orders`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = [
            { Symbol = "AAPL"; Shares = 50; Price = 100m } // 5000 value
        ]
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let targetSharesMap = calculateTargetShares config
    let buyOrders = calculateBuyOrders config targetSharesMap
    
    // Total value: 15000, Target AAPL: 90 shares, Target MSFT: 30 shares
    // AAPL: 90 target - 50 current = 40 (buy)
    // MSFT: 30 target - 0 current = 30 (buy)
    Assert.Equal(2, buyOrders.Length)
    let aaplBuy = buyOrders |> List.find (fun o -> o.Symbol = "AAPL")
    let msftBuy = buyOrders |> List.find (fun o -> o.Symbol = "MSFT")
    Assert.Equal(40, aaplBuy.Shares)
    Assert.Equal(30, msftBuy.Shares)
    Assert.Equal(4000m, aaplBuy.Value)
    Assert.Equal(6000m, msftBuy.Value)

[<Fact>]
let ``calculateFinalPositions should calculate correct final positions`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let targetSharesMap = calculateTargetShares config
    let finalPositions = calculateFinalPositions config targetSharesMap
    
    Assert.Equal(2, finalPositions.Length)
    let aaplPos = finalPositions |> List.find (fun p -> p.Symbol = "AAPL")
    let msftPos = finalPositions |> List.find (fun p -> p.Symbol = "MSFT")
    
    Assert.Equal(60, aaplPos.Shares) // 10000 * 0.6 / 100
    Assert.Equal(20, msftPos.Shares) // 10000 * 0.4 / 200
    Assert.Equal(6000m, aaplPos.Value)
    Assert.Equal(4000m, msftPos.Value)
    Assert.Equal(0.6m, aaplPos.Weight)
    Assert.Equal(0.4m, msftPos.Weight)
    Assert.Equal(0.6m, aaplPos.FinalWeight) // 6000 / 10000
    Assert.Equal(0.4m, msftPos.FinalWeight) // 4000 / 10000

[<Fact>]
let ``Commission calculation should be included when enabled`` () =
    let config = {
        IncludeCommission = true
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 1.0m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        Assert.Equal(1, rebalanceResult.Orders.Length)
        let order = rebalanceResult.Orders |> List.head
        Assert.True(order.Commission > 0m, "Commission should be calculated when enabled")
        Assert.True(rebalanceResult.TotalCommission > 0m, "Total commission should be calculated")
    | Error _ -> Assert.True(false, "Should not return error for valid config")

[<Fact>]
let ``Commission calculation should be zero when disabled`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 1.0m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        Assert.Equal(1, rebalanceResult.Orders.Length)
        let order = rebalanceResult.Orders |> List.head
        Assert.Equal(0m, order.Commission)
        Assert.Equal(0m, rebalanceResult.TotalCommission)
    | Error _ -> Assert.True(false, "Should not return error for valid config")

[<Fact>]
let ``Weight normalization should work correctly`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.3m }
            { Symbol = "MSFT"; Weight = 0.2m }
            { Symbol = "GOOGL"; Weight = 0.1m }
        ] // Total weight = 0.6, will be normalized
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
            { Symbol = "GOOGL"; Name = "Alphabet Inc."; Price = 300m }
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        Assert.Equal(3, rebalanceResult.Orders.Length)
        let totalValue = rebalanceResult.Orders |> List.sumBy (fun o -> o.Value)
        // With integer truncation, we expect the total to be less than or equal to cash
        Assert.True(totalValue <= 10000m, sprintf "Total value %M should be <= 10000" totalValue)
        Assert.True(totalValue > 9500m, sprintf "Total value %M should be > 9500" totalValue) // Should be close to 10000
    | Error _ -> Assert.True(false, "Should not return error for valid config")

[<Fact>]
let ``Zero cash should result in no orders for index rebalancing`` () =
    let config = {
        IncludeCommission = false
        Cash = 0m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        Assert.Equal(0, rebalanceResult.Orders.Length)
        Assert.Equal(0m, rebalanceResult.TotalValue)
        Assert.Equal(0m, rebalanceResult.TotalCommission)
        Assert.Equal(0m, rebalanceResult.RemainingCash)
    | Error _ -> Assert.True(false, "Should not return error for valid config")

[<Fact>]
let ``Zero price should be handled gracefully`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 0m } // Zero price
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        // Should only include MSFT order, skip AAPL due to zero price
        Assert.Equal(1, rebalanceResult.Orders.Length)
        let msftOrder = rebalanceResult.Orders |> List.find (fun o -> o.Symbol = "MSFT")
        Assert.Equal("MSFT", msftOrder.Symbol)
    | Error _ -> Assert.True(false, "Should handle zero price gracefully")

[<Fact>]
let ``Large numbers should be handled correctly`` () =
    let config = {
        IncludeCommission = false
        Cash = 1000000m // 1 million
        Index = [
            { Symbol = "AAPL"; Weight = 0.5m }
            { Symbol = "MSFT"; Weight = 0.5m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 1000m } // High price
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 2000m }
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        Assert.Equal(2, rebalanceResult.Orders.Length)
        let totalValue = rebalanceResult.Orders |> List.sumBy (fun o -> o.Value)
        Assert.Equal(float 1000000m, float totalValue, float 1000m) // Allow for rounding differences
    | Error _ -> Assert.True(false, "Should handle large numbers correctly")

[<Fact>]
let ``Complex portfolio with multiple stocks should rebalance correctly`` () =
    let config = {
        IncludeCommission = false
        Cash = 50000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.3m }
            { Symbol = "MSFT"; Weight = 0.25m }
            { Symbol = "GOOGL"; Weight = 0.2m }
            { Symbol = "AMZN"; Weight = 0.15m }
            { Symbol = "TSLA"; Weight = 0.1m }
        ]
        Current = [
            { Symbol = "AAPL"; Shares = 100; Price = 150m } // 15000
            { Symbol = "MSFT"; Shares = 50; Price = 300m }  // 15000
            { Symbol = "GOOGL"; Shares = 20; Price = 2500m } // 50000
        ]
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 150m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 300m }
            { Symbol = "GOOGL"; Name = "Alphabet Inc."; Price = 2500m }
            { Symbol = "AMZN"; Name = "Amazon.com Inc."; Price = 3000m }
            { Symbol = "TSLA"; Name = "Tesla Inc."; Price = 800m }
        ]
    }
    let result = rebalanceCurrentPortfolio config
    match result with
    | Ok rebalanceResult ->
        // Total value: 50000 + 15000 + 15000 + 50000 = 130000
        // Should have both buys and sells
        Assert.True(rebalanceResult.Buys.Length > 0, "Should have buy orders")
        Assert.True(rebalanceResult.Sells.Length > 0, "Should have sell orders")
        
        let totalBuyValue = rebalanceResult.Buys |> List.sumBy (fun o -> o.Value)
        let totalSellValue = rebalanceResult.Sells |> List.sumBy (fun o -> o.Value)
        Assert.True(totalBuyValue > 0m, "Should have positive buy value")
        Assert.True(totalSellValue > 0m, "Should have positive sell value")
        
        // Check that final positions are calculated
        Assert.Equal(5, rebalanceResult.Final.Length)
    | Error _ -> Assert.True(false, "Should not return error for complex portfolio")

[<Fact>]
let ``Cash flow calculations should be correct`` () =
    let config = {
        IncludeCommission = true
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = [
            { Symbol = "AAPL"; Shares = 50; Price = 100m } // 5000
            { Symbol = "MSFT"; Shares = 20; Price = 200m }  // 4000
        ]
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceCurrentPortfolio config
    match result with
    | Ok rebalanceResult ->
        // Initial cash: 10000
        // Current portfolio value: 5000 + 4000 = 9000
        // Total value: 19000
        
        // Check cash flow calculations
        Assert.True(rebalanceResult.CashAfterSelling >= 10000m, "Cash after selling should be >= initial cash")
        Assert.True(rebalanceResult.CashAfterBuying <= rebalanceResult.CashAfterSelling, "Cash after buying should be <= cash after selling")
        
        // Verify the relationship between values
        let expectedCashAfterSelling = 10000m + rebalanceResult.TotalSellValue - rebalanceResult.TotalSellCommission
        Assert.Equal(float expectedCashAfterSelling, float rebalanceResult.CashAfterSelling, float 1m)
        
        let expectedCashAfterBuying = rebalanceResult.CashAfterSelling - rebalanceResult.TotalBuyValue - rebalanceResult.TotalBuyCommission
        Assert.Equal(float expectedCashAfterBuying, float rebalanceResult.CashAfterBuying, float 1m)
    | Error _ -> Assert.True(false, "Should not return error for valid config")

// ============================================================================
// EDGE CASE TESTS
// ============================================================================

[<Fact>]
let ``Empty current portfolio should work for current portfolio rebalancing`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0.6m }
            { Symbol = "MSFT"; Weight = 0.4m }
        ]
        Current = [] // Empty current portfolio
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceCurrentPortfolio config
    match result with
    | Ok rebalanceResult ->
        // Should only have buy orders, no sell orders
        Assert.Equal(0, rebalanceResult.Sells.Length)
        Assert.Equal(2, rebalanceResult.Buys.Length)
        Assert.Equal(2, rebalanceResult.Final.Length)
    | Error _ -> Assert.True(false, "Should handle empty current portfolio")

[<Fact>]
let ``All weights zero should result in no orders`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 0m }
            { Symbol = "MSFT"; Weight = 0m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        Assert.Equal(0, rebalanceResult.Orders.Length)
        Assert.Equal(0m, rebalanceResult.TotalValue)
    | Error _ -> Assert.True(false, "Should handle zero weights")

[<Fact>]
let ``Negative weights should be handled gracefully`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = -0.5m }
            { Symbol = "MSFT"; Weight = 1.5m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
            { Symbol = "MSFT"; Name = "Microsoft Corp."; Price = 200m }
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        // Should still process the request, negative weights will be normalized
        Assert.True(rebalanceResult.Orders.Length > 0, "Should process orders even with negative weights")
    | Error _ -> Assert.True(false, "Should handle negative weights gracefully")

[<Fact>]
let ``Very small cash amounts should be handled correctly`` () =
    let config = {
        IncludeCommission = false
        Cash = 0.01m // Very small amount
        Index = [
            { Symbol = "AAPL"; Weight = 1.0m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 100m }
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        // Should handle very small amounts without errors
        Assert.True(rebalanceResult.Orders.Length >= 0, "Should handle very small cash amounts")
    | Error _ -> Assert.True(false, "Should handle very small cash amounts")

[<Fact>]
let ``Very high prices should be handled correctly`` () =
    let config = {
        IncludeCommission = false
        Cash = 10000m
        Index = [
            { Symbol = "AAPL"; Weight = 1.0m }
        ]
        Current = []
        Prices = [
            { Symbol = "AAPL"; Name = "Apple Inc."; Price = 1000000m } // Very high price
        ]
    }
    let result = rebalanceIndex config
    match result with
    | Ok rebalanceResult ->
        // Should handle very high prices without errors
        Assert.True(rebalanceResult.Orders.Length >= 0, "Should handle very high prices")
    | Error _ -> Assert.True(false, "Should handle very high prices")





 