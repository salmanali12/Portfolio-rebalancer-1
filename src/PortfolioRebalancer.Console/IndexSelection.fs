module PortfolioRebalancer.Console.IndexSelection

open System
open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Scraper.Scraper
open PortfolioRebalancer.Console.Table
open PortfolioRebalancer.Core.Logger
open PortfolioRebalancer.Console.Utilities
open Spectre.Console

let rec selectIndexFromList (indices: IndexInfo list) : string option =
    AnsiConsole.MarkupLine "\n[cyan]Available Indices:[/]"
    indices 
    |> List.iteri (fun i index -> 
        AnsiConsole.MarkupLine (sprintf "%d. %s (%.2f)" (i + 1) index.Name index.Current))
    
    printf "\nSelect an index number (or '0' to cancel): "
    let input = Console.ReadLine().Trim()
    
    match System.Int32.TryParse(input) with
    | true, num when num = 0 -> 
        AnsiConsole.MarkupLine "[yellow]Selection cancelled.[/]"
        None
    | true, num when num > 0 && num <= List.length indices -> 
        let selectedIndex = indices.[num - 1]
        AnsiConsole.MarkupLine (sprintf "[green]Selected: %s[/]" selectedIndex.Name)
        Some selectedIndex.Code
    | _ -> 
        AnsiConsole.MarkupLine "[red]Invalid selection. Please try again.[/]"
        selectIndexFromList indices

let showIndicesAndSelect () = async {
    AnsiConsole.MarkupLine "\n[cyan]📊 Fetching PSX indices...[/]"
    
    use driver = CreateChromeDriver()
    let! result = ScrapeIndicesTableAsync driver
    
    match result with
    | Ok indices ->
        displayIndicesTable indices
        printfn "" // Add a blank line for better readability
        let selectedIndex = selectIndexFromList indices
        
        match selectedIndex with
        | Some indexCode ->
            AnsiConsole.MarkupLine(sprintf "[green]✅ Selected index: %s[/]" indexCode)
            return Some indexCode
        | None ->
            AnsiConsole.MarkupLine "[yellow]❌ No index selected.[/]"
            return None
    | Error error ->
        AnsiConsole.MarkupLine "[red]❌ Failed to fetch indices:[/]"
        printErrors (Error error)
        return None
} 