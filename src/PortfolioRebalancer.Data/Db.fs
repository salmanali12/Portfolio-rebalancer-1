module PortfolioRebalancer.Data.Db

open System
open System.Threading.Tasks
open System.Collections.Generic
open Dapper
open Dapper.FSharp
open Dapper.FSharp.PostgreSQL
open Npgsql
open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Configuration
open PortfolioRebalancer.Core.Performance
open PortfolioRebalancer.Core.Resilience
open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.Logger

// Register Dapper type handlers
do OptionTypes.register()

let private getConnection() = 
    let logger: ILogger = LoggerContext.GetLoggerByName "Database"
    try
        new NpgsqlConnection(Database.ConnectionString)
    with
    | ex -> 
        logger.LogError(ex, "Failed to create database connection")
        reraise()

let ensureTablesAsync() = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Database"
    
    let operation = async {
        use conn = getConnection()
        do! conn.OpenAsync() |> Async.AwaitTask
        let sql = """
        CREATE TABLE IF NOT EXISTS "IndexTableRecord" (
            "Symbol" TEXT PRIMARY KEY,
            "Weight" NUMERIC(12,4) NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "StockRecord" (
            "Symbol" TEXT PRIMARY KEY,
            "Name" TEXT NOT NULL,
            "Price" NUMERIC(12,4) NOT NULL
        );
        CREATE TABLE IF NOT EXISTS "CurrentPortfolio" (
            "Id" SERIAL PRIMARY KEY,
            "Symbol" TEXT NOT NULL,
            "Shares" INTEGER NOT NULL,
            "Price" NUMERIC(12,8) NOT NULL
        );
        """
        do! conn.ExecuteAsync sql |> Async.AwaitTask |> Async.Ignore
        logger.LogInformation("Database tables ensured")
    }
    
    do! Retry.withCriticalRetry (fun () -> operation)
}

let indexTable = table<IndexTableRecord>

let setIndexRecordsAsync (records: IndexTableRecord list) = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Database"
    
    let operation = async {
        use conn = getConnection()
        do! conn.OpenAsync() |> Async.AwaitTask

        do! delete { for _ in indexTable do deleteAll } 
            |> conn.DeleteAsync 
            |> Async.AwaitTask 
            |> Async.Ignore

        // Only insert if we have records
        if not (List.isEmpty records) then
            do! insert { into indexTable; values records } 
                |> conn.InsertAsync 
                |> Async.AwaitTask 
                |> Async.Ignore
            logger.LogDebug("Updated {Count} index records and invalidated cache", records.Length)
        else
            logger.LogDebug("No index records to insert, only cleared existing data")
        
        // Invalidate cache after updating data
        cacheManager.Remove "index_records"
    }
    
    do! Retry.withCriticalRetry (fun () -> operation)
}

let getIndexRecordsAsync() = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Database"
    // Check cache first
    match cacheManager.Get<IndexTableRecord list> "index_records" with
    | Some cached ->
        logger.LogDebug("Retrieved index records from cache")
        return cached
    | None ->
        logger.LogDebug("Cache miss for index records, fetching from database")
        
        let operation = async {
            use conn = getConnection()
            do! conn.OpenAsync() |> Async.AwaitTask

            let! results = 
                select { for _ in indexTable do selectAll } 
                |> conn.SelectAsync<IndexTableRecord> 
                |> Async.AwaitTask

            let records = results |> Seq.toList
            // Cache for 10 minutes (index data doesn't change frequently)
            cacheManager.Set("index_records", records, 10)
            logger.LogDebug("Cached {Count} index records for 10 minutes", records.Length)
            return records
        }
        
        return! Retry.withPageRetry (fun () -> operation)
}

let stockRecordTable = table<StockRecord>

let setStockRecordsAsync (records: StockRecord list) = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Database"
    
    let operation = async {
        use conn = getConnection()
        do! conn.OpenAsync() |> Async.AwaitTask
            
        do! delete { for _ in stockRecordTable do deleteAll } 
            |> conn.DeleteAsync 
            |> Async.AwaitTask 
            |> Async.Ignore

        // Only insert if we have records
        if not (List.isEmpty records) then
            do! insert { into stockRecordTable; values records } 
                |> conn.InsertAsync 
                |> Async.AwaitTask 
                |> Async.Ignore
            logger.LogDebug("Updated {Count} stock records and invalidated cache", records.Length)
        else
            logger.LogDebug("No stock records to insert, only cleared existing data")
        
        // Invalidate cache after updating data
        cacheManager.Remove "stock_records"
    }
    
    do! Retry.withCriticalRetry (fun () -> operation)
}

let getStockRecordsAsync() = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Database"
    // Check cache first
    match cacheManager.Get<StockRecord list> "stock_records" with
    | Some cached ->
        logger.LogDebug("Retrieved stock records from cache")
        return cached
    | None ->
        logger.LogDebug("Cache miss for stock records, fetching from database")
        
        let operation = async {
            use conn = getConnection()
            do! conn.OpenAsync() |> Async.AwaitTask

            let! results = 
                select { for _ in stockRecordTable do selectAll } 
                |> conn.SelectAsync<StockRecord> 
                |> Async.AwaitTask

            let records = results |> Seq.toList
            // Cache for 2 minutes (stock prices change frequently)
            cacheManager.Set("stock_records", records, 2)
            logger.LogDebug("Cached {Count} stock records for 2 minutes", records.Length)
            return records
        }
        
        return! Retry.withPageRetry (fun () -> operation)
}

let currentPortfolioTable = table<CurrentPortfolio>

let setCurrentPortfolioAsync (portfolio: CurrentPortfolio list) = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Database"
    
    let operation = async {
        use conn = getConnection()
        do! conn.OpenAsync() |> Async.AwaitTask
            
        do! delete { for _ in currentPortfolioTable do deleteAll } 
            |> conn.DeleteAsync 
            |> Async.AwaitTask 
            |> Async.Ignore

        do! insert { into currentPortfolioTable; values portfolio } 
            |> conn.InsertAsync 
            |> Async.AwaitTask 
            |> Async.Ignore
        
        // Invalidate cache after updating data
        cacheManager.Remove "current_portfolio"
        logger.LogDebug("Updated {Count} current portfolio records and invalidated cache", portfolio.Length)
    }
    
    do! Retry.withCriticalRetry (fun () -> operation)
}

let getCurrentPortfolioAsync() = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Database"
    // Check cache first
    match cacheManager.Get<CurrentPortfolio list> "current_portfolio" with
    | Some cached ->
        logger.LogDebug("Retrieved current portfolio from cache")
        return cached
    | None ->
        logger.LogDebug("Cache miss for current portfolio, fetching from database")
        
        let operation = async {
            use conn = getConnection()
            do! conn.OpenAsync() |> Async.AwaitTask

            let! results = 
                select { for _ in currentPortfolioTable do selectAll } 
                |> conn.SelectAsync<CurrentPortfolio> 
                |> Async.AwaitTask

            let records = results |> Seq.toList
            // Cache for 5 minutes (portfolio changes when imported)
            cacheManager.Set("current_portfolio", records, 5)
            logger.LogDebug("Cached {Count} current portfolio records for 5 minutes", records.Length)
            return records
        }
        
        return! Retry.withPageRetry (fun () -> operation)
}