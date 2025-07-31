module PortfolioRebalancer.Core.ServiceContainer

open System
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Configuration
open Serilog
open Serilog.Configuration
open PortfolioRebalancer.Core.Configuration

// Functional service container - immutable and pure
type ServiceContainer = {
    ServiceProvider: IServiceProvider
}

// Pure function to create service container without builder pattern
let createServiceContainer (configBuilder: IConfigurationBuilder option) : ServiceContainer =
    let services = ServiceCollection()
    
    // Add configuration first if provided
    match configBuilder with
    | Some config -> 
        let configuration = config.Build()
        services.AddSingleton<IConfiguration> configuration |> ignore
        

        
        let logger = 
            LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger()
        
        // Log the configuration to verify it's working
        logger.Information("Service container created with configuration")
        logger.Debug("Logging configuration loaded - Debug level enabled")
        
        services.AddLogging(fun logging ->
            logging.ClearProviders().AddSerilog(logger, dispose = true) |> ignore) |> ignore
    | None -> 
        // Add logging with default Serilog configuration
        let logger = 
            LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .CreateLogger()
        
        services.AddLogging(fun logging ->
            logging.ClearProviders().AddSerilog(logger, dispose = true) |> ignore) |> ignore
    
    let serviceProvider = services.BuildServiceProvider()
    { ServiceProvider = serviceProvider }

// Pure function to create default service container
let createDefaultServiceContainer() : ServiceContainer =
    createServiceContainer (Some (createConfigurationBuilder()))

// Functional logger factory
let createLogger<'T> (container: ServiceContainer) =
    container.ServiceProvider.GetRequiredService<ILogger<'T>>()

let createLoggerByName (categoryName: string) (container: ServiceContainer) =
    container.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger categoryName

// Functional service resolution
let getService<'T when 'T: not struct> (container: ServiceContainer) : 'T option =
    match container.ServiceProvider.GetService<'T>() with
    | null -> None
    | service -> Some service

let getRequiredService<'T> (container: ServiceContainer) : 'T =
    container.ServiceProvider.GetRequiredService<'T>()

// Functional logger creation helpers
let getLogger<'T> (container: ServiceContainer) =
    createLogger<'T> container

let getLoggerByName (categoryName: string) (container: ServiceContainer)=
    createLoggerByName categoryName container 