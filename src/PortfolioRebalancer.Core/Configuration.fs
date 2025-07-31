module PortfolioRebalancer.Core.Configuration

open System
open System.IO
open System.Reflection
open Microsoft.Extensions.Configuration

// Shared configuration builder function to eliminate code duplication
let createConfigurationBuilder() : IConfigurationBuilder =
    let builder = ConfigurationBuilder()
    
    // Try to find the configuration files in multiple possible locations
    let possiblePaths = [
        IO.Directory.GetCurrentDirectory()
        IO.Path.GetDirectoryName(Reflection.Assembly.GetExecutingAssembly().Location)
        IO.Path.Combine(IO.Directory.GetCurrentDirectory(), "src", "PortfolioRebalancer.Console")
        IO.Path.Combine(IO.Directory.GetCurrentDirectory(), "PortfolioRebalancer.Tests")
        IO.Path.Combine(IO.Directory.GetCurrentDirectory(), "..", "PortfolioRebalancer.Console")
        IO.Path.Combine(IO.Directory.GetCurrentDirectory(), "..", "PortfolioRebalancer.Tests")
    ]
    
    let basePath = 
        possiblePaths 
        |> List.tryFind (fun path -> 
            IO.File.Exists(IO.Path.Combine(path, "appsettings.json")))
        |> Option.defaultValue (IO.Directory.GetCurrentDirectory())
    

    
    // Get the environment from environment variables
    let environment = 
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        |> Option.ofObj
        |> Option.defaultValue "Production"
    

    
    builder
        .SetBasePath(basePath)
        .AddJsonFile("appsettings.json", optional = true, reloadOnChange = true)
        .AddJsonFile(sprintf "appsettings.%s.json" environment, optional = true, reloadOnChange = true)
        .AddEnvironmentVariables()

// Global configuration instance
let private configuration : IConfiguration = 
    createConfigurationBuilder().Build()

// Commission calculation constants
module Commission =
    let MinimumPrice = configuration.GetValue<decimal>("Commission:MinimumPrice", 12m)
    let FixedRate = configuration.GetValue<decimal>("Commission:FixedRate", 0.03m)
    let PercentageRate = configuration.GetValue<decimal>("Commission:PercentageRate", 0.0025m)

// Database configuration
module Database =
    let ConnectionString = configuration.GetValue<string>("Database:ConnectionString", "Host=localhost;Port=5432;Database=portfolio;Username=postgres;Password=postgres1234")
    let DefaultTimeout = TimeSpan.FromSeconds(configuration.GetValue<float>("Database:DefaultTimeoutSeconds", 30.0))

module App =
    let DefaultCulture = configuration.GetValue<string>("App:DefaultCulture", "hi-IN")
    let CurrencySymbol = configuration.GetValue<string>("App:CurrencySymbol", "Rs.")

// Validation rules
module Validation =
    let MinimumCashAmount = configuration.GetValue<decimal>("Validation:MinimumCashAmount", 0m)
    let MaximumCashAmount = configuration.GetValue<decimal>("Validation:MaximumCashAmount", 1_000_000_000m)
    let MinimumShares = configuration.GetValue<int>("Validation:MinimumShares", 0)
    let MaximumShares = configuration.GetValue<int>("Validation:MaximumShares", 1_000_000_000)
    let MinimumPrice = configuration.GetValue<decimal>("Validation:MinimumPrice", 0.01m)
    let MaximumPrice = configuration.GetValue<decimal>("Validation:MaximumPrice", 1_000_000m)
    let MinimumWeight = configuration.GetValue<decimal>("Validation:MinimumWeight", 0m)
    let MaximumWeight = configuration.GetValue<decimal>("Validation:MaximumWeight", 1m)
    let MaxSymbolLength = configuration.GetValue<int>("Validation:MaxSymbolLength", 10)
    let MinSymbolLength = configuration.GetValue<int>("Validation:MinSymbolLength", 1)

// Logging configuration
module Logging =
    let DefaultLogLevel = configuration.GetValue<string>("Logging:LogLevel:Default", "Information")
    let MicrosoftLogLevel = configuration.GetValue<string>("Logging:LogLevel:Microsoft", "Warning")
    let IncludeScopes = configuration.GetValue<bool>("Logging:Console:IncludeScopes", true)
    let TimestampFormat = configuration.GetValue<string>("Logging:Console:TimestampFormat", "yyyy-MM-dd HH:mm:ss ")
    let FilePath = configuration.GetValue<string>("Logging:File:Path", "logs/portfolio-{Date}.log")
    let FileLogLevel = configuration.GetValue<string>("Logging:File:LogLevel:Default", "Information")
    let FileSizeLimitBytes = configuration.GetValue<int64>("Logging:File:FileSizeLimitBytes", 10485760L)
    let RetainedFileCountLimit = configuration.GetValue<int>("Logging:File:RetainedFileCountLimit", 31) 