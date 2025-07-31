module PortfolioRebalancer.Console.App

open System
open Microsoft.Extensions.Logging
open PortfolioRebalancer.Console.Menu
open PortfolioRebalancer.Console.Table
open PortfolioRebalancer.Console.Utilities
open PortfolioRebalancer.Core.Configuration
open PortfolioRebalancer.Core.ServiceContainer
open PortfolioRebalancer.Core.Logger
open PortfolioRebalancer.Data.Db
open Spectre.Console

let initializeApplication () = async {
    try
        createServiceContainer (Some (createConfigurationBuilder()))
            |> LoggerContext.Initialize
            
        AnsiConsole.MarkupLine "[bold blue]🚀 Starting Portfolio Rebalancer...[/]"
        
        AnsiConsole.MarkupLine "[cyan]📋 Initializing database...[/]"
        do! ensureTablesAsync()
        AnsiConsole.MarkupLine "[green]✅ Database initialized successfully![/]"
        
        return Ok ()
    with
    | ex ->
        AnsiConsole.MarkupLine(sprintf "\n[red]💥 Fatal issue: %s[/]" ex.Message)
        AnsiConsole.MarkupLine "[red]Application will exit. Please check your configuration and try again.[/]"
        return Error ex
}

let runMenuLoop () = async {
    let rec runMenu() = async {
        try
            displayMenu()
            let input = Console.ReadLine()
            let option = parseMenuOption input
            
            match option with
            | ShowPortfolio ->
                do! showSimpleCurrentPortfolio ()
                return true
                
            | ImportPortfolio ->
                do! handleImportPortfolio ()
                return true
                
            | RebalanceIndex ->
                AnsiConsole.MarkupLine "\n[yellow]⚖️  Index Rebalancing[/]"
                let includeCommission = getCommissionPreference()
                do! handleRebalancing "index" includeCommission
                return true
                
            | RebalanceCurrent ->
                AnsiConsole.MarkupLine "\n[magenta]🔄 Current Portfolio Rebalancing[/]"
                let includeCommission = getCommissionPreference()
                do! handleRebalancing "current" includeCommission
                return true
                
            | GetData ->
                do! handleGetData ()
                return true
                
            | ShowPerformance ->
                do! handleShowPerformance ()
                return true
                
            | ClearCache ->
                do! handleClearCache ()
                return true
                
            | Exit ->
                AnsiConsole.MarkupLine "\n[blue]👋 Thank you for using Portfolio Rebalancer![/]"
                return false
                
            | Invalid ->
                AnsiConsole.MarkupLine "\n[red]❌ Invalid option. Please select a valid menu option.[/]"
                return true
        with
        | ex ->
            AnsiConsole.MarkupLine(sprintf "\n[red]💥 An unexpected issue occurred: %s[/]" ex.Message)
            AnsiConsole.MarkupLine "[yellow]Please try again or contact support if the problem persists.[/]"
            return true
    }

    let rec runMenuLoop () = async {
        let! continueRunning = runMenu()
        if continueRunning then
            do! runMenuLoop()
    }
    
    do! runMenuLoop()
}

let runApplication () = async {
    let! initResult = initializeApplication()
    
    match initResult with
    | Ok _ ->
        do! runMenuLoop()
    | Error ex ->
        Environment.Exit(1)
} 