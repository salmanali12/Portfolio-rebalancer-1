module PortfolioRebalancer.Data.PortfolioImporter

open System
open System.IO
open System.Text.Json
open System.Text.Json.Serialization
open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Resilience
open PortfolioRebalancer.Data.Db
open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.Logger

let options = JsonSerializerOptions()
do
    options.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
    JsonFSharpOptions.Default().WithUnionAdjacentTag().AddToJsonSerializerOptions(options)
let importCurrentPortfolioFromJson (filePath: string) = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "PortfolioImporter"
    logger.LogInformation("Starting portfolio import from: {FilePath}", filePath)
    // Check if file exists
    if not (File.Exists filePath) then
        logger.LogError("File not found: {FilePath}", filePath)
        logger.LogError("Please ensure the file exists and the path is correct.")
        failwith (sprintf "File not found: %s" filePath)
    // Check if file is empty
    let fileInfo = FileInfo(filePath)
    if fileInfo.Length = 0L then
        logger.LogError("File is empty: {FilePath}", filePath)
        failwith (sprintf "File is empty: %s" filePath)
    let jsonContent = File.ReadAllText filePath
    logger.LogDebug("Read {Length} characters from file", jsonContent.Length)
    let portfolio = JsonSerializer.Deserialize<CurrentPortfolio list>(jsonContent, options)
    logger.LogInformation("Successfully deserialized {Count} portfolio items", portfolio.Length)
    // Validate that we have at least one record
    if List.isEmpty portfolio then
        logger.LogError("Portfolio file contains no records.")
        logger.LogError("Please ensure the JSON contains at least one portfolio entry.")
        failwith "Portfolio file contains no records."
    // Validate each record and filter out ETFs
    let validationErrors = 
        portfolio |> List.mapi (fun index record ->
            let errors = ResizeArray<string>()
            if String.IsNullOrWhiteSpace(record.Symbol) then
                errors.Add(sprintf "Record %d: Symbol is empty or null" (index + 1))
            if record.Shares <= 0 then
                errors.Add(sprintf "Record %d: Shares must be greater than 0 (got %d)" (index + 1) record.Shares)
            if record.Price <= 0m then
                errors.Add(sprintf "Record %d: Price must be greater than 0 (got %M)" (index + 1) record.Price)
            errors |> Seq.toList
        ) |> List.concat
    // Filter out ETFs (symbols ending with common ETF suffixes)
    let isETF (symbol:string) =
        symbol.EndsWith("ETF", StringComparison.OrdinalIgnoreCase) || 
        symbol.EndsWith("ETN", StringComparison.OrdinalIgnoreCase) || 
        symbol.EndsWith("ETP", StringComparison.OrdinalIgnoreCase) ||
        symbol.EndsWith("FUND", StringComparison.OrdinalIgnoreCase) ||
        symbol.EndsWith("TRUST", StringComparison.OrdinalIgnoreCase)
    let etfs, stocks = portfolio |> List.partition (fun record -> isETF record.Symbol)
    if not (List.isEmpty etfs) then
        logger.LogInformation("ETFs detected and will be excluded from import:")
        etfs |> List.iter (fun etf -> logger.LogInformation("  - {Symbol} (ETF)", etf.Symbol))
    if List.isEmpty stocks then
        logger.LogError("No valid stocks found after filtering ETFs.")
        logger.LogError("Please ensure your portfolio contains individual stocks, not just ETFs.")
        failwith "No valid stocks found after filtering ETFs."
    if not (List.isEmpty validationErrors) then
        logger.LogError("Validation errors found:")
        validationErrors |> List.iter (fun error -> logger.LogError("  - {Error}", error))
        let errorMsg = "Validation errors found:\n" + (String.concat "\n" validationErrors)
        failwith errorMsg
    // Import to database (only stocks, not ETFs)
    let operation = async {
        do! setCurrentPortfolioAsync stocks
        logger.LogInformation("Current portfolio imported and saved to database.")
        logger.LogInformation("  - File: {FilePath}", filePath)
        logger.LogInformation("  - Stocks imported: {Count}", stocks.Length)
        logger.LogInformation("  - ETFs excluded: {Count}", etfs.Length)
        logger.LogInformation("  - Total value: {Value}", stocks |> List.sumBy (fun r -> decimal r.Shares * r.Price))
    }
    
    do! Retry.withCriticalRetry (fun () -> operation)
    return ()
}

let exportCurrentPortfolioToJson (filePath: string) = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "PortfolioImporter"
    logger.LogInformation("Starting portfolio export to: {FilePath}", filePath)
    
    let operation = async {
        let! portfolio = getCurrentPortfolioAsync()
        let jsonContent = JsonSerializer.Serialize(portfolio, options)
        File.WriteAllText(filePath, jsonContent)
        logger.LogInformation("Current portfolio exported successfully.")
        logger.LogInformation("  - File: {FilePath}", filePath)
        logger.LogInformation("  - Records exported: {Count}", portfolio.Length)
    }
    
    do! Retry.withPageRetry (fun () -> operation)
    return ()
}