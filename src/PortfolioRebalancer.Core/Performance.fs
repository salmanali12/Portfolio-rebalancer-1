module PortfolioRebalancer.Core.Performance

open System
open System.Collections.Generic
open System.Collections.Concurrent
open System.Diagnostics
open PortfolioRebalancer.Core.Types
open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.Logger

// Simple in-memory cache
type Cache<'T> = {
    Data: 'T option
    Timestamp: DateTime
    ExpiryMinutes: int
}

type CacheManager() =
    let cache = ConcurrentDictionary<string, obj>()
    
    member _.Get<'T>(key: string) : 'T option =
        match cache.TryGetValue(key) with
        | true, value ->
            let cached = value :?> Cache<'T>
            if DateTime.Now.Subtract(cached.Timestamp).TotalMinutes < float cached.ExpiryMinutes then
                Some cached.Data.Value
            else
                cache.TryRemove(key) |> ignore
                None
        | false, _ -> None
    
    member _.Set<'T>(key: string, data: 'T, expiryMinutes: int) =
        let cacheEntry = {
            Data = Some data
            Timestamp = DateTime.Now
            ExpiryMinutes = expiryMinutes
        }
        cache.AddOrUpdate(key, cacheEntry :> obj, fun _ _ -> cacheEntry :> obj) |> ignore
    
    member _.Clear() = cache.Clear()
    
    member _.Remove(key: string) = cache.TryRemove(key) |> ignore

let cacheManager = CacheManager()

// Performance monitoring
type PerformanceMetrics = {
    OperationName: string
    Duration: TimeSpan
    Timestamp: DateTime
    Success: bool
}

let private performanceMetrics = ConcurrentQueue<PerformanceMetrics>()

let measureOperation<'T> (operationName: string) (operation: unit -> 'T) : 'T =
    let logger: ILogger = LoggerContext.GetLoggerByName "Performance"
    let stopwatch = Stopwatch.StartNew()
    let timestamp = DateTime.Now
    
    try
        let result = operation()
        stopwatch.Stop()
        
        let metrics = {
            OperationName = operationName
            Duration = stopwatch.Elapsed
            Timestamp = timestamp
            Success = true
        }
        
        performanceMetrics.Enqueue(metrics)
        logger.LogDebug("Operation '{OperationName}' completed in {Duration}", operationName, stopwatch.Elapsed)
        result
    with
    | ex ->
        stopwatch.Stop()
        let metrics = {
            OperationName = operationName
            Duration = stopwatch.Elapsed
            Timestamp = timestamp
            Success = false
        }
        
        performanceMetrics.Enqueue(metrics)
        logger.LogError(ex, "Operation '{OperationName}' failed after {Duration}", operationName, stopwatch.Elapsed)
        reraise()

// Note: Async operation measurement removed due to F# version compatibility

// Get performance statistics
let getPerformanceStats() : PerformanceMetrics list =
    performanceMetrics.ToArray() |> Array.toList

// Clear old metrics (older than specified hours)
let clearOldMetrics (hoursOld: int) =
    let logger: ILogger = LoggerContext.GetLoggerByName "Performance"
    let cutoffTime = DateTime.Now.AddHours(-float hoursOld)
    
    let rec processMetrics (removed: int) : int =
        if performanceMetrics.IsEmpty then
            removed
        else
            match performanceMetrics.TryDequeue() with
            | true, metric when metric.Timestamp < cutoffTime ->
                processMetrics (removed + 1)
            | true, metric ->
                // Put it back if it's not old enough
                performanceMetrics.Enqueue(metric)
                removed
            | false, _ -> 
                removed
    
    let removed = processMetrics 0
    logger.LogInformation("Cleared {Count} old performance metrics", removed) 