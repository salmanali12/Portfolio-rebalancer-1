module PortfolioRebalancer.Core.Logger

open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.ServiceContainer
open Microsoft.Extensions.DependencyInjection

type LoggerContext private () =
    static let mutable container: ServiceContainer option = None
    
    static member Initialize(serviceContainer: ServiceContainer) =
        container <- Some serviceContainer

    static member GetLogger<'T>() =
        match container with
        | Some c -> getLogger<'T> c
        | None -> failwith "Logger not initialized. Call LoggerContext.Initialize first."
    
    static member GetLoggerByName(name: string) =
        match container with
        | Some c -> getLoggerByName name c
        | None -> failwith "Logger not initialized. Call LoggerContext.Initialize first."
