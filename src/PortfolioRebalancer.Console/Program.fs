open System
open PortfolioRebalancer.Console.App

[<EntryPoint>]
let main argv = 
    Async.RunSynchronously(runApplication())
    0