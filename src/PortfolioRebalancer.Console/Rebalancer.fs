module PortfolioRebalancer.Console.Rebalancer

open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Helpers
open PortfolioRebalancer.Core.RebalancingCalculations
open PortfolioRebalancer.Console.Table
open PortfolioRebalancer.Core.Performance
open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.Logger
open Spectre.Console

// Main rebalancing function with improved structure
let rebalanceAndDisplay 
    (kind: string) 
    (includeCommission: bool) 
    (cash: decimal) 
    (index: IndexTableRecord list) 
    (current: CurrentPortfolio list) 
    (prices: StockRecord list) =
    let logger: ILogger = LoggerContext.GetLoggerByName "Rebalancer"
    
    // Helper functions for better organization
    let findMissingSymbols () = 
        let priceMap = createPriceMap prices
        let indexSymbols = index |> List.map (fun i -> i.Symbol)
        let currentSymbols = current |> List.map (fun c -> c.Symbol)
        let allSymbols =
            if kind = "index" then indexSymbols
            else Set.union (Set.ofList indexSymbols) (Set.ofList currentSymbols) |> Set.toList
        allSymbols |> List.filter (fun sym -> not (priceMap.ContainsKey sym))
    
    let validateInputs () =
        let missingSymbols = findMissingSymbols()
        
        // Show warning for missing symbols but continue
        if not (List.isEmpty missingSymbols) then
            logger.LogWarning("No price data for symbols: {Symbols}", String.concat ", " missingSymbols)
            AnsiConsole.MarkupLine(sprintf "[yellow][WARNING] No price data for: %s[/]" (String.concat ", " missingSymbols))
        
        // Check for required data
        if List.isEmpty index then
            logger.LogError("No index records found")
            AnsiConsole.MarkupLine "[red]❌ No index records found.[/]"
            false
        elif List.isEmpty prices then
            logger.LogError("No stock price records found")
            AnsiConsole.MarkupLine "[red]❌ No stock price records found.[/]"
            false
        elif kind = "current" && List.isEmpty current then
            logger.LogError("No current portfolio records found")
            AnsiConsole.MarkupLine "[red]❌ No current portfolio records found.[/]"
            false
        else
            true
    
    let createConfig () = { 
        IncludeCommission = includeCommission
        Cash = cash
        Index = index
        Current = current
        Prices = prices
    }
    
    let handleIndexRebalancing config =
        logger.LogInformation("Starting index rebalancing with cash: {Cash}", cash)
        match rebalanceIndex config with
        | Ok result ->
            logger.LogInformation("Index rebalancing completed successfully")
            let weightMap = createWeightMap index
            let displayOrders = result.Orders |> List.map (fun order -> 
                {| 
                    Symbol = order.Symbol
                    Shares = order.Shares
                    Value = order.Value
                    Commission = order.Commission
                    Price = order.Price
                    Weight = Map.tryFind order.Symbol weightMap |> Option.defaultValue 0m
                |})
            showRebalanceIndexResult displayOrders includeCommission cash
        | Error _ -> 
            logger.LogError("Failed to rebalance index")
            AnsiConsole.MarkupLine "[red]❌ Failed to rebalance index.[/]"
    
    let handleCurrentPortfolioRebalancing config =
        logger.LogInformation("Starting current portfolio rebalancing with cash: {Cash}", cash)
        match rebalanceCurrentPortfolio config with
        | Ok result ->
            logger.LogInformation("Current portfolio rebalancing completed successfully")
            let displaySells = result.Sells |> List.map (fun order -> 
                {| 
                    Symbol = order.Symbol
                    Shares = order.Shares
                    Price = order.Price
                    Value = order.Value
                    Commission = order.Commission
                |})
            let displayBuys = result.Buys |> List.map (fun order -> 
                {| 
                    Symbol = order.Symbol
                    Shares = order.Shares
                    Price = order.Price
                    Value = order.Value
                    Commission = order.Commission
                |})
            let displayFinal = result.Final |> List.map (fun pos -> 
                {| 
                    Symbol = pos.Symbol
                    Shares = pos.Shares
                    Value = pos.Value
                    Commission = pos.Commission
                    Price = pos.Price
                    Weight = pos.Weight
                    FinalWeight = pos.FinalWeight
                |})
            showRebalanceCurrentPortfolioResult displaySells displayBuys displayFinal includeCommission cash
        | Error _ -> 
            logger.LogError("Failed to rebalance current portfolio")
            AnsiConsole.MarkupLine "[red]❌ Failed to rebalance current portfolio.[/]"
    
    // Main execution flow
    if validateInputs() then
        let config = createConfig()
        if kind = "index" then
            handleIndexRebalancing config
        else
            handleCurrentPortfolioRebalancing config