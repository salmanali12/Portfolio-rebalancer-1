module PortfolioRebalancer.Console.Menu

open System
open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Helpers
open PortfolioRebalancer.Scraper.Scraper
open PortfolioRebalancer.Console.Table
open PortfolioRebalancer.Console.IndexSelection
open PortfolioRebalancer.Console.Utilities
open PortfolioRebalancer.Data.Db
open PortfolioRebalancer.Data.PortfolioImporter
open PortfolioRebalancer.Core.Performance
open PortfolioRebalancer.Core.Logger
open PortfolioRebalancer.Console.Rebalancer
open Spectre.Console

// Global variable to store selected index
let mutable selectedIndexCode: string option = None

type MenuOption = 
    | ShowPortfolio
    | ImportPortfolio
    | RebalanceIndex
    | RebalanceCurrent
    | GetData
    | ShowPerformance
    | ClearCache
    | Exit
    | Invalid

let parseMenuOption (input: string) : MenuOption =
    match input.Trim() with
    | "1" -> ShowPortfolio
    | "2" -> ImportPortfolio
    | "3" -> RebalanceIndex
    | "4" -> RebalanceCurrent
    | "5" -> GetData
    | "6" -> ShowPerformance
    | "7" -> ClearCache
    | "0" -> Exit
    | _ -> Invalid

let displayMenu () =
    let selectedIndexText = 
        match selectedIndexCode with
        | Some code -> sprintf " (Current: %s)" code
        | None -> " (None selected)"
    
    let menuTable = 
        Table()
            .Title("[bold blue]Portfolio Rebalancer[/]")
            .AddColumns([|"Option"; "Description"|])
            .AddRow("1", "Show Index and Current Portfolio")
            .AddRow("2", "Import Current Portfolio from JSON")
            .AddRow("3", "Rebalance Index with Cash (optionally show commission)")
            .AddRow("4", "Rebalance Current Portfolio with Cash (optionally show)")
            .AddRow("5", sprintf "Get Index and Stock Data%s" selectedIndexText)
            .AddRow("6", "Show Performance Metrics")
            .AddRow("7", "Clear Cache")
            .AddRow("0", "Exit")
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Blue)
    
    AnsiConsole.Write(menuTable)
    printf "\nSelect an option: "

let handleRebalancing (kind: string) (includeCommission: bool) =
    match getCashAmount() with
    | Some cash ->
        async {
            let operationName = sprintf "%s_rebalancing" kind
            let! index = getIndexRecordsAsync()
            let! prices = getStockRecordsAsync()
            
            if kind = "index" then
                rebalanceAndDisplay kind includeCommission cash index [] prices
            else
                let! current = getCurrentPortfolioAsync()
                rebalanceAndDisplay kind includeCommission cash index current prices
        }
    | None -> async { return () }

let handleShowPerformance () = async {
    printfn "\n📈 Performance Metrics"
    let stats = getPerformanceStats()
    if List.isEmpty stats then
        printfn "No performance metrics available yet."
    else
        let performanceTable = 
            Table()
                .Title("[bold green]Recent Performance Metrics[/]")
                .AddColumns([|"Status"; "Operation"; "Duration"; "Time"|])
                .Border(TableBorder.Rounded)
                .BorderColor Color.Green
        
        stats 
        |> List.take 10
        |> List.iter (fun metric ->
            let status = if metric.Success then "[green]✅[/]" else "[red]❌[/]"
            let operation = metric.OperationName
            let duration = metric.Duration.ToString()
            let time = metric.Timestamp.ToString "HH:mm:ss"
            performanceTable.AddRow(status, operation, duration, time) |> ignore)
        
        AnsiConsole.Write(performanceTable)
    return ()
}

let handleClearCache () = async {
    AnsiConsole.MarkupLine "\n[orange3]🗑️  Cache Management[/]"
    let confirmed = getConfirmation "Are you sure you want to clear the cache?"
    if confirmed then
        cacheManager.Clear()
        clearOldMetrics 24
        AnsiConsole.MarkupLine "[green]✅ Cache and old metrics cleared successfully![/]"
    else
        AnsiConsole.MarkupLine "[red]❌ Cache clearing cancelled.[/]"
    return ()
}

let handleImportPortfolio () = async {
    AnsiConsole.MarkupLine "\n[cyan]📥 Importing portfolio from JSON...[/]"
    printf "Enter the path to your portfolio JSON file: "
    let filePath = Console.ReadLine().Trim()
    
    if String.IsNullOrWhiteSpace filePath then
        AnsiConsole.MarkupLine "[red]❌ No file path provided. Import cancelled.[/]"
    else
        do! importCurrentPortfolioFromJson filePath
    return ()
}

let handleGetData () = async {
    AnsiConsole.MarkupLine "\n[green]🌐 Fetching market data...[/]"
    let logger: ILogger = LoggerContext.GetLoggerByName "Menu"
    logger.LogDebug("🔍 Starting data fetch operation...")
    
    // First, show indices table and let user select
    let! selectedIndex = showIndicesAndSelect()
    
    match selectedIndex with
    | Some indexCode ->
        selectedIndexCode <- Some indexCode
        AnsiConsole.MarkupLine(sprintf "\n[cyan]Using selected index: %s[/]" indexCode)
        
        let! result = ConstructIndexCache true indexCode
        logger.LogDebug("✅ Data fetch operation completed")
        printErrors result
    | None ->
        AnsiConsole.MarkupLine "\n[yellow]⚠️ No index selected, using default: KSE30[/]"
        let! result = ConstructIndexCache true "KSE30"
        logger.LogDebug("✅ Data fetch operation completed")
        printErrors result
    
    return ()
} 