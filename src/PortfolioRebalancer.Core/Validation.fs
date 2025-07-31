module PortfolioRebalancer.Core.Validation

open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Configuration

// Enhanced validation functions with comprehensive rules
let validateCashAmount (cash: decimal) : ValidationResult<CashAmount> =
    match cash with
    | c when c < Validation.MinimumCashAmount -> Error (InvalidCashAmount c)
    | c when c > Validation.MaximumCashAmount -> Error (InvalidCashAmount c)
    | c -> Ok (CashAmount c)

let validateSymbol (symbol: string) : ValidationResult<Symbol> =
    match symbol with
    | null -> Error (InvalidSymbol "")
    | s when System.String.IsNullOrWhiteSpace s -> Error (InvalidSymbol s)
    | s when s.Length < Validation.MinSymbolLength -> Error (InvalidSymbol s)
    | s when s.Length > Validation.MaxSymbolLength -> Error (InvalidSymbol s)
    | s -> Ok (Symbol s)

let validatePrice (price: decimal) : ValidationResult<Price> =
    match price with
    | p when p < Validation.MinimumPrice -> Error (InvalidPrice p)
    | p when p > Validation.MaximumPrice -> Error (InvalidPrice p)
    | p -> Ok (Price p)

let validateShares (shares: int) : ValidationResult<ShareCount> =
    match shares with
    | s when s < Validation.MinimumShares -> Error (InvalidShares s)
    | s when s > Validation.MaximumShares -> Error (InvalidShares s)
    | s -> Ok (ShareCount s)

let validateWeight (weight: decimal) : ValidationResult<Weight> =
    match weight with
    | w when w < Validation.MinimumWeight -> Error (InvalidWeight w)
    | w when w > Validation.MaximumWeight -> Error (InvalidWeight w)
    | w -> Ok (Weight w)

let validateIndexNotEmpty (index: IndexTableRecord list) : Result<IndexTableRecord list, ValidationError> =
    if List.isEmpty index then
        Error EmptyIndex
    else
        Ok index

let validateRebalanceConfig (config: RebalanceConfig) : Result<ValidatedRebalanceConfig, ValidationError> =
    match validateCashAmount config.Cash, validateIndexNotEmpty config.Index with
    | Ok _, Ok _ ->
        Ok {
            IncludeCommission = config.IncludeCommission
            Cash = config.Cash
            Index = config.Index
            Current = config.Current
            Prices = config.Prices
            MissingSymbols = []
        }
    | Error e, _ -> Error e
    | _, Error e -> Error e

// Helper for Result computation expressions
type ResultBuilder() =
    member _.Return(x) = Ok x
    member _.ReturnFrom(m) = m
    member _.Bind(m, f) = Result.bind f m
    member _.Zero() = Ok ()

let result = ResultBuilder() 