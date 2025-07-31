module PortfolioRebalancer.Core.RebalancingCalculations

open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Helpers
open PortfolioRebalancer.Core.Validation

// Helper functions for price and weight mapping
let createPriceMap (prices: StockRecord list) =
    prices |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList

let createWeightMap (index: IndexTableRecord list) =
    index |> List.map (fun i -> i.Symbol, i.Weight) |> Map.ofList

let createCurrentSharesMap (current: CurrentPortfolio list) =
    current |> List.map (fun c -> c.Symbol, c.Shares) |> Map.ofList

// Calculate target shares for each symbol
let calculateTargetShares (config: RebalanceConfig) =
    let priceMap = createPriceMap config.Prices
    let weightMap = createWeightMap config.Index
    let currentMap = createCurrentSharesMap config.Current
    
    let totalValue = 
        config.Cash + 
        (config.Current |> List.sumBy (fun c -> 
            match Map.tryFind c.Symbol priceMap with 
            | Some p -> decimal c.Shares * p 
            | _ -> 0m))
    
    let totalWeight = config.Index |> List.sumBy (fun i -> i.Weight)
    
    config.Index
    |> List.choose (fun idx ->
        match Map.tryFind idx.Symbol priceMap with
        | Some price when price > 0m ->
            let targetValue = totalValue * (idx.Weight / totalWeight)
            let shares = int (targetValue / price)
            Some (idx.Symbol, shares)
        | _ -> None)
    |> Map.ofList



// Calculate sell orders
let calculateSellOrders (config: RebalanceConfig) (targetSharesMap: Map<string, int>) =
    let priceMap = createPriceMap config.Prices
    let currentMap = createCurrentSharesMap config.Current
    
    config.Current
    |> List.choose (fun current ->
        let targetShares = Map.tryFind current.Symbol targetSharesMap |> Option.defaultValue 0
        match Map.tryFind current.Symbol priceMap with
        | Some price when price > 0m && current.Shares > targetShares ->
            let shares = current.Shares - targetShares
            let value = decimal shares * price
            let commission = if config.IncludeCommission then calculateCommission price shares else 0m
            Some { 
                Symbol = current.Symbol
                Shares = shares
                Price = price
                Value = value
                Commission = commission
            }
        | _ -> None)

// Calculate buy orders
let calculateBuyOrders (config: RebalanceConfig) (targetSharesMap: Map<string, int>) =
    let priceMap = createPriceMap config.Prices
    let currentMap = createCurrentSharesMap config.Current
    
    targetSharesMap
    |> Map.toList
    |> List.choose (fun (symbol, targetShares) ->
        let currentShares = Map.tryFind symbol currentMap |> Option.defaultValue 0
        match Map.tryFind symbol priceMap with
        | Some price when price > 0m && targetShares > currentShares ->
            let shares = targetShares - currentShares
            let value = decimal shares * price
            let commission = if config.IncludeCommission then calculateCommission price shares else 0m
            Some { 
                Symbol = symbol
                Shares = shares
                Price = price
                Value = value
                Commission = commission
            }
        | _ -> None)

// Calculate final portfolio positions
let calculateFinalPositions (config: RebalanceConfig) (targetSharesMap: Map<string, int>) =
    let priceMap = createPriceMap config.Prices
    let weightMap = createWeightMap config.Index
    
    let totalValue = 
        config.Cash + 
        (config.Current |> List.sumBy (fun c -> 
            match Map.tryFind c.Symbol priceMap with 
            | Some p -> decimal c.Shares * p 
            | _ -> 0m))
    
    targetSharesMap
    |> Map.toList
    |> List.choose (fun (symbol, shares) ->
        match Map.tryFind symbol priceMap with
        | Some price when price > 0m ->
            let value = decimal shares * price
            let weight = Map.tryFind symbol weightMap |> Option.defaultValue 0m
            let finalWeight = if totalValue > 0m then value / totalValue else 0m
            Some { 
                Symbol = symbol
                Shares = shares
                Value = value
                Commission = 0m
                Price = price
                Weight = weight
                FinalWeight = finalWeight
            }
        | _ -> None)

// Main rebalancing calculation for current portfolio
let rebalanceCurrentPortfolio (config: RebalanceConfig) : Result<RebalanceResult, ValidationError> =
    result {
        let! validatedConfig = validateRebalanceConfig config
        let targetSharesMap = calculateTargetShares config
        let sells = calculateSellOrders config targetSharesMap
        let buys = calculateBuyOrders config targetSharesMap
        let final = calculateFinalPositions config targetSharesMap
        
        let totalSellValue = sells |> List.sumBy (fun r -> r.Value)
        let totalSellCommission = sells |> List.sumBy (fun r -> r.Commission)
        let cashAfterSelling = config.Cash + totalSellValue - totalSellCommission
        
        let totalBuyValue = buys |> List.sumBy (fun r -> r.Value)
        let totalBuyCommission = buys |> List.sumBy (fun r -> r.Commission)
        let cashAfterBuying = cashAfterSelling - totalBuyValue - totalBuyCommission
        
        return { 
            Sells = sells
            Buys = buys
            Final = final
            CashAfterSelling = cashAfterSelling
            CashAfterBuying = cashAfterBuying
            TotalSellValue = totalSellValue
            TotalBuyValue = totalBuyValue
            TotalSellCommission = totalSellCommission
            TotalBuyCommission = totalBuyCommission
        }
    }

// Rebalancing calculation for index (no current portfolio)
let rebalanceIndex (config: RebalanceConfig) : Result<IndexRebalanceResult, ValidationError> =
    result {
        let! validatedConfig = validateRebalanceConfig config
        let priceMap = createPriceMap config.Prices
        let totalWeight = config.Index |> List.sumBy (fun i -> i.Weight)
        
        // Handle edge cases: zero cash or zero total weight
        if config.Cash = 0m || totalWeight = 0m then
            return { 
                Orders = []
                TotalValue = 0m
                TotalCommission = 0m
                RemainingCash = config.Cash
            }
        else
            let orders = 
                config.Index
                |> List.choose (fun idx ->
                    match Map.tryFind idx.Symbol priceMap with
                    | Some price when price > 0m ->
                        let normalizedWeight = idx.Weight / totalWeight
                        let targetValue = config.Cash * normalizedWeight
                        let shares = int (targetValue / price)
                        let value = decimal shares * price
                        let commission = if config.IncludeCommission then calculateCommission price shares else 0m
                        Some { 
                            Symbol = idx.Symbol
                            Shares = shares
                            Price = price
                            Value = value
                            Commission = commission
                        }
                    | _ -> None)
            
            let totalValue = orders |> List.sumBy (fun r -> r.Value)
            let totalCommission = orders |> List.sumBy (fun r -> r.Commission)
            let remainingCash = config.Cash - totalValue - totalCommission
            
            return { 
                Orders = orders
                TotalValue = totalValue
                TotalCommission = totalCommission
                RemainingCash = remainingCash
            }
    } 