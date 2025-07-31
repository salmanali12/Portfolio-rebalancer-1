module PortfolioRebalancer.Console.Utilities

open System
open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Helpers
open Spectre.Console

let printErrors (result: Result<unit, ScrapingError>) =
    match result with
    | Error error ->
        AnsiConsole.MarkupLine "[red]❌ Operation failed:[/]"
        match error with
        | NavigationError msg -> AnsiConsole.MarkupLine(sprintf "[red]🧭 Navigation Issue: %s[/]" msg)
        | ElementNotFound msg -> AnsiConsole.MarkupLine(sprintf "[red]🔍 Element Not Found: %s[/]" msg)
        | DataExtractionError msg -> AnsiConsole.MarkupLine(sprintf "[red]📊 Data Extraction Issue: %s[/]" msg)
        | PortfolioCreationError msg -> AnsiConsole.MarkupLine(sprintf "[red]📁 Portfolio Creation Issue: %s[/]" msg)
        | FileOperationError msg -> AnsiConsole.MarkupLine(sprintf "[red]📂 File Operation Issue: %s[/]" msg)
        | TimeoutError msg -> AnsiConsole.MarkupLine(sprintf "[red]⏰ Timeout Issue: %s[/]" msg)
        | NetworkError msg -> AnsiConsole.MarkupLine(sprintf "[red]🌐 Network Issue: %s[/]" msg)
    | _ -> ()

let rec getCashAmount () : decimal option =
    printf "Enter cash amount: "
    match Console.ReadLine() with
    | ValidDecimal cash when cash >= 0m -> Some cash
    | _ -> 
        printfn "Invalid cash amount. Please enter a valid positive number."
        getCashAmount()

let rec getCommissionPreference () : bool =
    printf "Include commission? (y/n): "
    let commissionInput = Console.ReadLine().Trim().ToLower()
    match commissionInput with
    | "y" | "yes" -> true
    | "n" | "no" -> false
    | _ -> 
        printfn "Please enter 'y' for yes or 'n' for no."
        getCommissionPreference()

let rec getConfirmation (message: string) : bool =
    printf "%s (y/n): " message
    let input = Console.ReadLine().Trim().ToLower()
    match input with
    | "y" | "yes" -> true
    | "n" | "no" -> false
    | _ -> 
        printfn "Please enter 'y' for yes or 'n' for no."
        getConfirmation message 