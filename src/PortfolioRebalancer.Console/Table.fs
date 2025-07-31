module PortfolioRebalancer.Console.Table

open System
open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.Logger
open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Helpers
open PortfolioRebalancer.Data.Db
open Spectre.Console

let showSimpleCurrentPortfolio () = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Table"
    logger.LogInformation("Loading portfolio data for display")
    let! indexRecords = getIndexRecordsAsync()
    let! stockRecords = getStockRecordsAsync()
    let! records = getCurrentPortfolioAsync()
    
    // Display index table with enhanced styling
    let indexTable =
        Table()
            .Title("[bold blue]📊 Index Records[/]")
            .AddColumns([|"Symbol"; "Weight %"; "Price"|])
            .Border(TableBorder.Rounded)
            .BorderColor Color.Blue
    
    let priceMap = stockRecords |> List.map (fun s -> s.Symbol, s.Price) |> Map.ofList
    
    let indexRows = 
        indexRecords 
        |> List.sortByDescending (fun s -> s.Weight)
        |> List.map (fun s ->
            let price = Map.tryFind s.Symbol priceMap |> Option.defaultValue 0m
            let weightPct = s.Weight * 100m
            let weightDisplay = sprintf "%.2f%%" weightPct
            s.Symbol, weightDisplay, formatPKR price)
    
    match indexRows with
    | [] -> 
        indexTable.AddRow("[red]No data[/]", "-", "-") |> ignore
    | rows -> 
        rows |> List.iter (fun (symbol, weight, price) -> 
            let styledSymbol = sprintf "[bold cyan]%s[/]" symbol
            let styledWeight = sprintf "[green]%s[/]" weight
            let styledPrice = sprintf "[yellow]%s[/]" price
            indexTable.AddRow(styledSymbol, styledWeight, styledPrice) |> ignore)
    
    AnsiConsole.Write(indexTable)
    logger.LogDebug("Displayed {Count} index records", List.length indexRows)
    
    // Display current portfolio table with enhanced styling
    let table =
        Table()
            .Title("[bold green]💼 My Current Portfolio[/]")
            .AddColumns([|"Symbol"; "Shares"; "Buy Price"; "Current Price"; "Value"; "Gain/Loss"; "Gain/Loss %"|])
            .Border(TableBorder.Rounded)
            .BorderColor Color.Green
    
    let portfolioRows =
        records
        |> List.map (fun s ->
            let currentPrice = Map.tryFind s.Symbol priceMap |> Option.defaultValue 0m
            let value = decimal s.Shares * currentPrice
            let cost = decimal s.Shares * s.Price
            let gainLoss = value - cost
            let gainLossPct = if cost <> 0m then gainLoss / cost else 0m
            (s, currentPrice, value, cost, gainLoss, gainLossPct))
        |> List.sortByDescending (fun (_,_,value,_,_,_) -> value)
        |> List.map (fun (s, currentPrice, value, cost, gainLoss, gainLossPct) ->
            (s.Symbol, string s.Shares, formatPKR s.Price, formatPKR currentPrice, 
             formatPKR value, formatPKR gainLoss, toPercentage gainLossPct, gainLoss, gainLossPct))
    
    match portfolioRows with
    | [] -> 
        table.AddRow("[red]No data[/]", "-", "-", "-", "-", "-", "-") |> ignore
    | rows -> 
        rows |> List.iter (fun (symbol, shares, buyPrice, currentPrice, value, gainLoss, gainLossPct, gainLossRaw, gainLossPctRaw) -> 
            let styledSymbol = sprintf "[bold cyan]%s[/]" symbol
            let styledShares = sprintf "[blue]%s[/]" shares
            let styledBuyPrice = sprintf "[yellow]%s[/]" buyPrice
            let styledCurrentPrice = sprintf "[yellow]%s[/]" currentPrice
            let styledValue = sprintf "[bold white]%s[/]" value
            
            // Color code gain/loss based on performance
            let styledGainLoss = 
                if gainLossRaw >= 0m then sprintf "[green]%s[/]" gainLoss
                else sprintf "[red]%s[/]" gainLoss
            let styledGainLossPct = 
                if gainLossPctRaw >= 0m then sprintf "[green]%s[/]" gainLossPct
                else sprintf "[red]%s[/]" gainLossPct
            
            table.AddRow(styledSymbol, styledShares, styledBuyPrice, styledCurrentPrice, styledValue, styledGainLoss, styledGainLossPct) |> ignore)
    
    AnsiConsole.Write(table)
    logger.LogDebug("Displayed {Count} portfolio records", List.length portfolioRows)
    
    // Display summary with enhanced styling
    let totalCost = records |> List.sumBy (fun s -> decimal s.Shares * s.Price)
    let totalValue = records |> List.sumBy (fun s -> 
        let currentPrice = Map.tryFind s.Symbol priceMap |> Option.defaultValue 0m
        decimal s.Shares * currentPrice)
    let totalGainLoss = totalValue - totalCost
    let totalGainLossPct = if totalCost <> 0m then totalGainLoss / totalCost else 0m
    
    // Create summary table
    let summaryTable =
        Table()
            .Title("[bold magenta]📈 Portfolio Summary[/]")
            .AddColumns([|"Metric"; "Value"|])
            .Border(TableBorder.Rounded)
            .BorderColor Color.Magenta3
    
    let styledTotalCost = sprintf "[yellow]%s[/]" (formatPKR totalCost)
    let styledTotalValue = sprintf "[bold white]%s[/]" (formatPKR totalValue)
    let styledTotalGainLoss = 
        if totalGainLoss >= 0m then sprintf "[green]%s[/]" (formatPKR totalGainLoss)
        else sprintf "[red]%s[/]" (formatPKR totalGainLoss)
    let styledTotalGainLossPct = 
        if totalGainLossPct >= 0m then sprintf "[green]%s[/]" (toPercentage totalGainLossPct)
        else sprintf "[red]%s[/]" (toPercentage totalGainLossPct)
    
    summaryTable
        .AddRow("Total Invested", styledTotalCost)
        .AddRow("Current Value", styledTotalValue)
        .AddRow("Gain/Loss", styledTotalGainLoss)
        .AddRow("Gain/Loss %", styledTotalGainLossPct)
        |> ignore
    
    AnsiConsole.Write summaryTable
    logger.LogInformation("Portfolio display completed successfully")
    
    return ()
}



let showRebalanceIndexResult (results: {| Symbol: string; Shares: int; Value: decimal; Commission: decimal; Price: decimal; Weight: decimal |} list) (includeCommission: bool) (cash: decimal) =
    let totalValueAll = results |> List.sumBy (fun r -> r.Value)
    
    let table =
        if includeCommission then
            Table().Title("[bold blue]⚖️ Rebalance Index With Cash (With Commission)[/]")
                .AddColumns([|"Symbol"; "Index Weight"; "Current Weight"; "Price"; "Shares"; "Value"; "Commission"|])
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Blue)
        else
            Table().Title("[bold blue]⚖️ Rebalance Index With Cash (No Commission)[/]")
                .AddColumns([|"Symbol"; "Index Weight"; "Current Weight"; "Price"; "Shares"; "Value"|])
                .Border(TableBorder.Rounded)
                .BorderColor Color.Blue
    
    let sorted = results |> List.sortByDescending (fun r -> r.Value)
    
    let rows = 
        sorted 
        |> List.map (fun r ->
            let currentWeight = if totalValueAll > 0m then r.Value / totalValueAll else 0m
            if includeCommission then
                let styledSymbol = sprintf "[bold cyan]%s[/]" r.Symbol
                let styledIndexWeight = sprintf "[green]%s[/]" (toPercentage r.Weight)
                let styledCurrentWeight = sprintf "[blue]%s[/]" (toPercentage currentWeight)
                let styledPrice = sprintf "[yellow]%s[/]" (formatPKR r.Price)
                let styledShares = sprintf "[blue]%d[/]" r.Shares
                let styledValue = sprintf "[bold white]%s[/]" (formatPKR r.Value)
                let styledCommission = sprintf "[orange3]%s[/]" (formatPKR r.Commission)
                [styledSymbol; styledIndexWeight; styledCurrentWeight; styledPrice; styledShares; styledValue; styledCommission]
            else
                let styledSymbol = sprintf "[bold cyan]%s[/]" r.Symbol
                let styledIndexWeight = sprintf "[green]%s[/]" (toPercentage r.Weight)
                let styledCurrentWeight = sprintf "[blue]%s[/]" (toPercentage currentWeight)
                let styledPrice = sprintf "[yellow]%s[/]" (formatPKR r.Price)
                let styledShares = sprintf "[blue]%d[/]" r.Shares
                let styledValue = sprintf "[bold white]%s[/]" (formatPKR r.Value)
                [styledSymbol; styledIndexWeight; styledCurrentWeight; styledPrice; styledShares; styledValue])
    
    rows |> List.iter (fun row -> table.AddRow(row |> Array.ofList) |> ignore)
    
    table.AddEmptyRow() |> ignore
    
    let totalValue = sorted |> List.sumBy (fun r -> r.Value)
    let totalCommission = sorted |> List.sumBy (fun r -> r.Commission)
    
    if includeCommission then
        let styledTotalValue = sprintf "[bold green]%s[/]" (formatPKR totalValue)
        let styledTotalCommission = sprintf "[bold orange3]%s[/]" (formatPKR totalCommission)
        table.AddRow("[bold]Total[/]", "", "", "", "", styledTotalValue, styledTotalCommission) |> ignore
    else
        let styledTotalValue = sprintf "[bold green]%s[/]" (formatPKR totalValue)
        table.AddRow("[bold]Total[/]", "", "", "", "", styledTotalValue) |> ignore
    
    AnsiConsole.Write table
    
    let spent = if includeCommission then sorted |> List.sumBy (fun r -> r.Value + r.Commission) else sorted |> List.sumBy (fun r -> r.Value)
    let remaining = cash - spent
    
    // Create summary table
    let summaryTable =
        Table()
            .Title("[bold blue]💰 Rebalancing Summary[/]")
            .AddColumns([|"Metric"; "Value"|])
            .Border(TableBorder.Rounded)
            .BorderColor Color.Blue
    
    let styledSpent = sprintf "[yellow]%s[/]" (formatPKR spent)
    let styledRemaining = sprintf "[green]%s[/]" (formatPKR remaining)
    
    summaryTable
        .AddRow("Total spent on stocks", styledSpent)
        .AddRow("Remaining cash", styledRemaining)
        |> ignore
    
    if includeCommission then
        let styledCommission = sprintf "[orange3]%s[/]" (formatPKR totalCommission)
        summaryTable.AddRow("Total commission", styledCommission) |> ignore
    
    AnsiConsole.Write summaryTable

let showRebalanceCurrentPortfolioResult
    (sells: {| Symbol: string; Shares: int; Price: decimal; Value: decimal; Commission: decimal |} list)
    (buys: {| Symbol: string; Shares: int; Price: decimal; Value: decimal; Commission: decimal |} list)
    (final: {| Symbol: string; Shares: int; Value: decimal; Commission: decimal; Price: decimal; Weight: decimal; FinalWeight: decimal |} list)
    (includeCommission: bool)
    (cash: decimal) =
    
    let logger: ILogger = LoggerContext.GetLoggerByName "Table"
    logger.LogInformation("Displaying current portfolio rebalancing results with {SellCount} sells, {BuyCount} buys, {FinalCount} final positions", sells.Length, buys.Length, final.Length)
    
    // Display sell orders with enhanced styling
    let sortedSells = sells |> List.sortByDescending (fun r -> r.Value)
    let sellTable =
        if includeCommission then
            Table().Title("[bold red]📉 Sell Orders[/]")
                .AddColumns([|"Symbol"; "Shares"; "Price"; "Value"; "Commission"|])
                .Border(TableBorder.Rounded)
                .BorderColor Color.Red
        else
            Table().Title("[bold red]📉 Sell Orders[/]")
                .AddColumns([|"Symbol"; "Shares"; "Price"; "Value"|])
                .Border(TableBorder.Rounded)
                .BorderColor Color.Red
    
    let sellRows = 
        sortedSells 
        |> List.map (fun r ->
            let styledSymbol = sprintf "[bold cyan]%s[/]" r.Symbol
            let styledShares = sprintf "[red]%d[/]" r.Shares
            let styledPrice = sprintf "[yellow]%s[/]" (formatPKR r.Price)
            let styledValue = sprintf "[bold red]%s[/]" (formatPKR r.Value)
            if includeCommission then
                let styledCommission = sprintf "[orange3]%s[/]" (formatPKR r.Commission)
                [styledSymbol; styledShares; styledPrice; styledValue; styledCommission]
            else
                [styledSymbol; styledShares; styledPrice; styledValue])
    
    sellRows |> List.iter (fun row -> sellTable.AddRow(row |> Array.ofList) |> ignore)
    AnsiConsole.Write sellTable
    logger.LogDebug("Displayed sell orders table with {SellCount} orders", sortedSells.Length)
    
    let totalSellValue = sortedSells |> List.sumBy (fun r -> r.Value)
    let totalSellCommission = sortedSells |> List.sumBy (fun r -> r.Commission)
    let cashAfterSelling = 
        if includeCommission then 
            cash + totalSellValue - totalSellCommission
        else 
            cash + totalSellValue
    
    // Create sell summary table
    let sellSummaryTable =
        Table()
            .Title("[bold red]📉 Sell Summary[/]")
            .AddColumns([|"Metric"; "Value"|])
            .Border(TableBorder.Rounded)
            .BorderColor Color.Red
    
    let styledCashAfterSelling = sprintf "[green]%s[/]" (formatPKR cashAfterSelling)
    
    sellSummaryTable
        .AddRow("Cash after selling", styledCashAfterSelling)
        |> ignore
    
    if includeCommission then
        let styledTotalSellCommission = sprintf "[orange3]%s[/]" (formatPKR totalSellCommission)
        sellSummaryTable.AddRow("Total sell commission", styledTotalSellCommission) |> ignore
    
    AnsiConsole.Write sellSummaryTable
    
    // Display buy orders with enhanced styling
    let sortedBuys = buys |> List.sortByDescending (fun r -> r.Value)
    let buyTable =
        if includeCommission then
            Table().Title("[bold green]📈 Buy Orders[/]")
                .AddColumns([|"Symbol"; "Shares"; "Price"; "Value"; "Commission"|])
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Green)
        else
            Table().Title("[bold green]📈 Buy Orders[/]")
                .AddColumns([|"Symbol"; "Shares"; "Price"; "Value"|])
                .Border(TableBorder.Rounded)
                .BorderColor Color.Green
    
    let buyRows = 
        sortedBuys 
        |> List.map (fun r ->
            let styledSymbol = sprintf "[bold cyan]%s[/]" r.Symbol
            let styledShares = sprintf "[green]%d[/]" r.Shares
            let styledPrice = sprintf "[yellow]%s[/]" (formatPKR r.Price)
            let styledValue = sprintf "[bold green]%s[/]" (formatPKR r.Value)
            if includeCommission then
                let styledCommission = sprintf "[orange3]%s[/]" (formatPKR r.Commission)
                [styledSymbol; styledShares; styledPrice; styledValue; styledCommission]
            else
                [styledSymbol; styledShares; styledPrice; styledValue])
    
    buyRows |> List.iter (fun row -> buyTable.AddRow(row |> Array.ofList) |> ignore)
    AnsiConsole.Write buyTable
    logger.LogDebug("Displayed buy orders table with {BuyCount} orders", sortedBuys.Length)
    
    let totalBuyValue = sortedBuys |> List.sumBy (fun r -> r.Value)
    let totalBuyCommission = sortedBuys |> List.sumBy (fun r -> r.Commission)
    let cashAfterBuying = 
        if includeCommission then 
            cashAfterSelling - totalBuyValue - totalBuyCommission
        else 
            cashAfterSelling - totalBuyValue
    
    // Create buy summary table
    let buySummaryTable =
        Table()
            .Title("[bold green]📈 Buy Summary[/]")
            .AddColumns([|"Metric"; "Value"|])
            .Border(TableBorder.Rounded)
            .BorderColor Color.Green
    
    let styledCashAfterBuying = sprintf "[green]%s[/]" (formatPKR cashAfterBuying)
    
    buySummaryTable
        .AddRow("Cash after buying", styledCashAfterBuying)
        |> ignore
    
    if includeCommission then
        let styledTotalBuyCommission = sprintf "[orange3]%s[/]" (formatPKR totalBuyCommission)
        buySummaryTable.AddRow("Total buy commission", styledTotalBuyCommission) |> ignore
    
    AnsiConsole.Write buySummaryTable
    
    // Display final portfolio with enhanced styling
    let finalTable =
        if includeCommission then
            Table().Title("[bold magenta]🎯 Final Portfolio After Rebalance[/]")
                .AddColumns([|"Symbol"; "Index Weight"; "Current Weight"; "Price"; "Shares"; "Value"; "Commission"|])
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Magenta3)
        else
            Table().Title("[bold magenta]🎯 Final Portfolio After Rebalance[/]")
                .AddColumns([|"Symbol"; "Index Weight"; "Current Weight"; "Price"; "Shares"; "Value"|])
                .Border(TableBorder.Rounded)
                .BorderColor Color.Magenta3
    
    let sortedFinal = final |> List.sortByDescending (fun r -> r.Value)
    
    let finalRows = 
        sortedFinal 
        |> List.map (fun r ->
            let styledSymbol = sprintf "[bold cyan]%s[/]" r.Symbol
            let styledIndexWeight = sprintf "[green]%s[/]" (toPercentage r.Weight)
            let styledCurrentWeight = sprintf "[blue]%s[/]" (toPercentage r.FinalWeight)
            let styledPrice = sprintf "[yellow]%s[/]" (formatPKR r.Price)
            let styledShares = sprintf "[white]%d[/]" r.Shares
            let styledValue = sprintf "[bold white]%s[/]" (formatPKR r.Value)
            if includeCommission then
                let styledCommission = sprintf "[orange3]%s[/]" (formatPKR r.Commission)
                [styledSymbol; styledIndexWeight; styledCurrentWeight; styledPrice; styledShares; styledValue; styledCommission]
            else
                [styledSymbol; styledIndexWeight; styledCurrentWeight; styledPrice; styledShares; styledValue])
    
    finalRows |> List.iter (fun row -> finalTable.AddRow(row |> Array.ofList) |> ignore)
    
    finalTable.AddEmptyRow() |> ignore
    logger.LogDebug("Displayed final portfolio table with {FinalCount} positions", sortedFinal.Length)
    
    let totalValue = sortedFinal |> List.sumBy (fun r -> r.Value)
    let totalCommission = sortedFinal |> List.sumBy (fun r -> r.Commission)
    
    if includeCommission then
        let styledTotalValue = sprintf "[bold green]%s[/]" (formatPKR totalValue)
        let styledTotalCommission = sprintf "[bold orange3]%s[/]" (formatPKR totalCommission)
        finalTable.AddRow("[bold]Total[/]", "", "", "", "", styledTotalValue, styledTotalCommission) |> ignore
    else
        let styledTotalValue = sprintf "[bold green]%s[/]" (formatPKR totalValue)
        finalTable.AddRow("[bold]Total[/]", "", "", "", "", styledTotalValue) |> ignore
    
    AnsiConsole.Write finalTable
    
    let spent = if includeCommission then sortedFinal |> List.sumBy (fun r -> r.Value + r.Commission) else sortedFinal |> List.sumBy (fun r -> r.Value)
    let remaining = cashAfterBuying
    
    // Create final summary table
    let finalSummaryTable =
        Table()
            .Title("[bold magenta]💰 Final Rebalancing Summary[/]")
            .AddColumns([|"Metric"; "Value"|])
            .Border(TableBorder.Rounded)
            .BorderColor Color.Magenta3
    
    let styledSpent = sprintf "[yellow]%s[/]" (formatPKR spent)
    let styledRemaining = sprintf "[green]%s[/]" (formatPKR remaining)
    
    finalSummaryTable
        .AddRow("Total spent on stocks", styledSpent)
        .AddRow("Remaining cash", styledRemaining)
        |> ignore
    
    if includeCommission then
        let styledCommission = sprintf "[orange3]%s[/]" (formatPKR totalCommission)
        finalSummaryTable.AddRow("Total commission", styledCommission) |> ignore
    
    AnsiConsole.Write finalSummaryTable
    logger.LogInformation("Portfolio display completed successfully")

let displayIndicesTable (indices: IndexInfo list) =
    let table = 
        Table()
            .Title("[bold green]PSX Market Indices[/]")
            .AddColumns([|"Index"; "Current"; "Change"; "% Change"; "High"; "Low"|])
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
    
    indices 
    |> List.iter (fun index ->
        let changeIcon = if index.Change >= 0m then "▲" else "▼"
        let changeText = sprintf "%s %.2f" changeIcon (abs index.Change)
        let changePercentText = sprintf "%s %.2f%%" changeIcon (abs index.ChangePercent)
        
        table.AddRow(
            index.Name,
            sprintf "%.2f" index.Current,
            changeText,
            changePercentText,
            sprintf "%.2f" index.High,
            sprintf "%.2f" index.Low
        ) |> ignore)
    
    AnsiConsole.Write(table)