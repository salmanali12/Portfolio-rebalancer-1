module PortfolioRebalancer.Core.Resilience

open System
open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.Logger

/// Retry policy configuration
type RetryConfig = {
    MaxRetries: int
    BaseDelay: TimeSpan
    MaxDelay: TimeSpan
    BackoffCoefficient: float
}

/// Default retry configurations for different scenarios
module RetryConfigs =
    /// Fast retries for UI interactions (clicking, navigation)
    let FastUI = {
        MaxRetries = 3
        BaseDelay = TimeSpan.FromSeconds(1.0)
        MaxDelay = TimeSpan.FromSeconds(5.0)
        BackoffCoefficient = 2.0
    }
    
    /// Medium retries for element finding
    let ElementFinding = {
        MaxRetries = 5
        BaseDelay = TimeSpan.FromSeconds(2.0)
        MaxDelay = TimeSpan.FromSeconds(15.0)
        BackoffCoefficient = 1.5
    }
    
    /// Slow retries for page loading and data extraction
    let PageLoading = {
        MaxRetries = 3
        BaseDelay = TimeSpan.FromSeconds(3.0)
        MaxDelay = TimeSpan.FromSeconds(20.0)
        BackoffCoefficient = 2.0
    }
    
    /// Aggressive retries for critical operations
    let Critical = {
        MaxRetries = 7
        BaseDelay = TimeSpan.FromSeconds(1.0)
        MaxDelay = TimeSpan.FromSeconds(30.0)
        BackoffCoefficient = 1.8
    }

/// Simple retry implementation without external dependencies
module Retry =
    let private logger: ILogger = LoggerContext.GetLoggerByName "Resilience"
    
    /// Check if an exception should trigger a retry
    let private shouldRetry (ex: Exception) =
        ex.Message.Contains("Element not found") ||
        ex.Message.Contains("timeout") ||
        ex.Message.Contains("Timed out") ||
        ex.Message.Contains("NoSuchElementException") ||
        ex.Message.Contains("WebDriverException") ||
        ex.Message.Contains("StaleElementReferenceException") ||
        ex.Message.Contains("ElementClickInterceptedException") ||
        // Database-specific retryable exceptions
        ex.Message.Contains("connection") ||
        ex.Message.Contains("Connection") ||
        ex.Message.Contains("timeout") ||
        ex.Message.Contains("Timeout") ||
        ex.Message.Contains("deadlock") ||
        ex.Message.Contains("Deadlock") ||
        ex.Message.Contains("temporary") ||
        ex.Message.Contains("Temporary") ||
        ex.Message.Contains("network") ||
        ex.Message.Contains("Network") ||
        ex.Message.Contains("socket") ||
        ex.Message.Contains("Socket")
    
    /// Calculate delay for retry attempt
    let private calculateDelay (config: RetryConfig) (retryAttempt: int) =
        let delay = 
            TimeSpan.FromMilliseconds(
                float config.BaseDelay.TotalMilliseconds * 
                (config.BackoffCoefficient ** float (retryAttempt - 1))
            )
        if delay > config.MaxDelay then config.MaxDelay else delay
    
    /// Execute with retry logic
    let withRetry<'T> (config: RetryConfig) (operation: unit -> Async<'T>) : Async<'T> =
        let rec executeWithRetry attempt =
            async {
                try
                    logger.LogDebug("Executing operation (attempt {Attempt}/{MaxAttempts})", attempt, config.MaxRetries)
                    return! operation()
                with
                | ex when shouldRetry ex && attempt < config.MaxRetries ->
                    let delay = calculateDelay config attempt
                    logger.LogWarning("Operation failed (attempt {Attempt}/{MaxAttempts}), retrying after {Delay}ms: {Exception}", 
                        attempt, config.MaxRetries, delay.TotalMilliseconds, ex.Message)
                    do! Async.Sleep (int delay.TotalMilliseconds)
                    return! executeWithRetry (attempt + 1)
                | ex ->
                    logger.LogError("Operation failed after {Attempt} attempts: {Exception}", attempt, ex.Message)
                    return raise ex
            }
        
        executeWithRetry 1
    
    /// Execute with fast UI retry (for clicking, navigation)
    let withFastRetry operation = withRetry RetryConfigs.FastUI operation
    
    /// Execute with element finding retry (for finding elements)
    let withElementRetry operation = withRetry RetryConfigs.ElementFinding operation
    
    /// Execute with page loading retry (for page loads, data extraction)
    let withPageRetry operation = withRetry RetryConfigs.PageLoading operation
    
    /// Execute with critical retry (for important operations)
    let withCriticalRetry operation = withRetry RetryConfigs.Critical operation 