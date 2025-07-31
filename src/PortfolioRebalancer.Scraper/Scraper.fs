module PortfolioRebalancer.Scraper.Scraper

open System
open System.Collections.ObjectModel
open OpenQA.Selenium
open OpenQA.Selenium.Chrome
open OpenQA.Selenium.Support.UI
open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Helpers
open PortfolioRebalancer.Core.Resilience
open PortfolioRebalancer.Data.Db
open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.Logger

let CreateChromeDriver () =
    let options = ChromeOptions()
    options.AddArgument "--headless"
    options.AddArgument "--no-sandbox"
    options.AddArgument "--disable-dev-shm-usage"
    options.AddArgument "--disable-extensions"
    options.AddArgument "--disable-plugins"
    options.AddArgument "--disable-images"
    options.AddArgument "--disable-web-security"
    options.AddArgument "--disable-features=VizDisplayCompositor"
    options.AddArgument "--log-level=3"
    options.AddArgument "--disable-logging"
    options.SetLoggingPreference(LogType.Browser, LogLevel.Off)
    let logger: ILogger = LoggerContext.GetLoggerByName "Scraper"
    logger.LogInformation "Creating ChromeDriver with headless configuration..."
    let driver = new ChromeDriver(options)
    logger.LogInformation "ChromeDriver created successfully"
    driver

let WaitForElementAsync (driver: IWebDriver) (by: By) (timeoutSeconds: float) = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Scraper"
    logger.LogDebug("🔍 Waiting for element: {Element} (timeout: {Timeout} seconds)", by, timeoutSeconds)
    
    let operation = async {
        let wait = WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds))
        let element = wait.Until(fun d -> d.FindElement(by))
        logger.LogDebug("✅ Element found successfully: {Element}", by)
        return element
    }
    
    try
        let! element = Retry.withElementRetry (fun () -> operation)
        return Ok element
    with
    | ex -> 
        logger.LogError(ex, "❌ Element not found after retries: {Element}", by)
        return Error (ElementNotFound ex.Message)
}

let ExecuteScriptAsync<'T> (driver: IWebDriver) (script: string) (args: obj array) = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Scraper"
    logger.LogDebug("🔄 Executing JavaScript: {Script}", script)
    
    let operation = async {
        let result = (driver :?> IJavaScriptExecutor).ExecuteScript(script, args)
        logger.LogDebug "✅ JavaScript executed successfully"
        return unbox<'T> result
    }
    
    try
        let! result = Retry.withFastRetry (fun () -> operation)
        return Ok result
    with
    | ex -> 
        logger.LogError(ex, "❌ JavaScript execution failed: {Script}", script)
        return Error (DataExtractionError ex.Message)
}

let ExtractStockDataFromTable (rows: ReadOnlyCollection<IWebElement>) =
    let logger: ILogger = LoggerContext.GetLoggerByName "Scraper"
    try
        logger.LogDebug("🔍 Extracting data from {RowCount} rows", rows.Count)
        
        let stockData = 
            rows 
            |> Seq.toList
            |> List.mapi (fun index row ->
                let cells = row.FindElements(By.TagName("td"))
                logger.LogDebug("Row {Index}: Found {CellCount} cells", index, cells.Count)
                
                // Log all cells for debugging
                for i in 0..(cells.Count - 1) do
                    let cellText = cells.[i].Text.Trim()
                    logger.LogDebug("Row {Index} Cell {CellIndex}: '{CellText}'", index, i, cellText)
                
                if cells.Count >= 7 then
                    let symbol = cells.[0].Text.Trim()
                    let name = cells.[1].Text.Trim()
                    let priceText = cells.[3].Text.Trim()  // CURRENT column
                    let weightText = cells.[6].Text.Trim() // IDX WTG (%) column
                    
                    logger.LogDebug("Row {Index}: Symbol='{Symbol}', Name='{Name}', Price='{Price}', Weight='{Weight}'", 
                        index, symbol, name, priceText, weightText)
                    
                    match weightText, priceText with
                    | ValidWeight weight, ValidDecimal price when not (String.IsNullOrEmpty(symbol)) ->
                        logger.LogDebug("✅ Row {Index}: Valid data extracted", index)
                        Some {
                            Symbol = symbol
                            Weight = weight
                            Price = price
                            Name = name
                        }
                    | _, _ -> 
                        logger.LogDebug("❌ Row {Index}: Invalid data - Weight='{Weight}', Price='{Price}'", index, weightText, priceText)
                        None
                else 
                    logger.LogDebug("❌ Row {Index}: Not enough cells ({CellCount} < 4)", index, cells.Count)
                    None)
            |> List.choose id
        
        logger.LogDebug("📊 Extracted {Count} valid records out of {Total} rows", List.length stockData, rows.Count)
        Ok stockData
    with
    | ex -> Error (DataExtractionError ex.Message)

let ScrapeIndexDataAsync (driver: IWebDriver) (url: string) (code: string) = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Scraper"
    
    let operation = async {
        logger.LogInformation("🚀 Starting to scrape index data for {Code} from: {Url}", code, url)
        driver.Navigate().GoToUrl url
        logger.LogInformation("✅ Navigated to: {Url}", url)
        logger.LogDebug("Current page title: {Title}", driver.Title)
        logger.LogDebug("Current page URL: {Url}", driver.Url)

        logger.LogDebug("Looking for KSE link with data-code='{Code}'", code)
        let! kseLink = WaitForElementAsync driver (By.CssSelector $"a[data-code='{code}']") 15.0
        
        match kseLink with
        | Error err -> 
            logger.LogError("Failed to find KSE link for {Code}", code)
            return Error err
        | Ok element ->
            logger.LogDebug("Found KSE link for {Code}, attempting to click", code)
            let! clickResult = ExecuteScriptAsync<obj> driver "arguments[0].click();" [| element |]
            
            match clickResult with
            | Error err -> 
                logger.LogError("Failed to click KSE link for {Code}", code)
                return Error err
            | Ok _ ->
                logger.LogInformation("Clicked {Code} link successfully", code)
                logger.LogDebug("Waiting 3 seconds for page to load...")
                do! Async.Sleep 3000
                logger.LogDebug("After click - Current page title: {Title}", driver.Title)
                logger.LogDebug("After click - Current page URL: {Url}", driver.Url)
                
                logger.LogDebug("Looking for table length selector...")
                let! selectElement = WaitForElementAsync driver (By.Name "DataTables_Table_1_length") 15.0
                
                match selectElement with
                | Error err -> 
                    logger.LogError("Failed to find table length selector")
                    return Error err
                | Ok element ->
                    logger.LogDebug("Found table length selector, changing to 100 rows...")
                    let select = SelectElement(element)
                    select.SelectByValue("100")
                    logger.LogInformation("Changed table length to 100")
                    logger.LogDebug("Waiting 3 seconds for table to reload...")
                    do! Async.Sleep 3000

                    
                    logger.LogDebug("Looking for data table...")
                    let! tableElement = WaitForElementAsync driver (By.Id "DataTables_Table_1") 15.0
                    
                    match tableElement with
                    | Error err -> 
                        logger.LogError("Failed to find data table")
                        return Error err
                    | Ok table ->
                        logger.LogDebug("Found data table, extracting rows...")
                        
                        // Extract and log table headers
                        let headers = table.FindElements(By.CssSelector("thead th"))
                        logger.LogDebug("Table headers found: {Count} columns", headers.Count)
                        for i in 0..(headers.Count - 1) do
                            let headerText = headers.[i].Text.Trim()
                            logger.LogDebug("🔍 Table Header {Index}: '{HeaderText}'", i, headerText)
                        
                        let rows = table.FindElements(By.CssSelector("tbody tr"))
                        logger.LogInformation("Found {Count} rows in table", rows.Count)
                        let htmlLength = table.GetAttribute("outerHTML").Length
                        let maxPreviewLength = 500
                        let previewLength = min maxPreviewLength htmlLength
                        logger.LogDebug("Table HTML structure: {Html}", table.GetAttribute("outerHTML").Substring(0, previewLength))
                        
                        logger.LogDebug("Extracting stock data from table rows...")
                        let stockDataResult = ExtractStockDataFromTable rows
                        
                        match stockDataResult with
                        | Error err -> 
                            logger.LogError("Failed to extract stock data: {Error}", err.ToString())
                            return Error err
                        | Ok stockData ->
                            logger.LogInformation("Successfully extracted {Count} stock records", List.length stockData)
                            let stockCount = List.length stockData
                            let maxCount = 3
                            let takeCount = min maxCount stockCount
                            let firstFew = stockData |> List.take takeCount
                            logger.LogDebug("First few records: {Records}", firstFew)
                            return Ok stockData
    }
    
    try
        let! result = Retry.withPageRetry (fun () -> operation)
        return result
    with
    | ex -> 
        logger.LogError(ex, "❌ Scraping error occurred after retries")
        return Error (NavigationError $"Scraping error: {ex.Message}")
}

let GetStockFromPSXAsync (driver: IWebDriver) (symbol: string) = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Scraper"
    
    let operation = async {
        let url = sprintf "https://dps.psx.com.pk/company/%s" symbol
        driver.Navigate().GoToUrl url
        logger.LogDebug("Fetching price for {Symbol} from {Url}", symbol, url)
    
        do! Async.Sleep 2000
    
        let! priceElement = WaitForElementAsync driver (By.CssSelector "div.quote__close") 10.0
        let! nameElementFull = WaitForElementAsync driver (By.CssSelector "div.quote__name") 10.0

        match priceElement, nameElementFull with
        | Ok pElement, Ok nElementFull ->
            let! nameResult = ExecuteScriptAsync<string> driver "return arguments[0].childNodes[0].textContent.trim();" [|nElementFull|]
            
            match nameResult with
            | Ok name ->
                let priceText = pElement.Text.Replace("Rs.", "").Replace(",", "").Trim()

                match priceText, name with
                | ValidDecimal price, nameText when not (String.IsNullOrEmpty(nameText)) -> 
                    logger.LogDebug("Found price for {Symbol}: {Price}", symbol, formatPKR price)
                    return Ok (price, nameText)
                | _, _ -> 
                    logger.LogWarning("Could not parse price for {Symbol}: {Price} or name error {Name}", symbol, priceText, name)
                    return Error (DataExtractionError $"Invalid price format for {symbol}: {priceText}")
            | Error err ->
                logger.LogWarning("Could not extract name for {Symbol}", symbol)
                return Error err
        | Error err, _ -> 
            logger.LogWarning("Could not find price element for {Symbol}", symbol)
            return Error err
        | _, Error err -> 
            logger.LogWarning("Could not find name element for {Symbol}", symbol)
            return Error err
    }
    
    try
        let! result = Retry.withPageRetry (fun () -> operation)
        return result
    with
    | ex -> 
        logger.LogError(ex, "Error getting price for {Symbol} after retries", symbol)
        return Error (DataExtractionError $"Failed to get price for {symbol}: {ex.Message}")
}

let ScrapeIndicesTableAsync (driver: IWebDriver) = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Scraper"
    
    let operation = async {
        logger.LogInformation("🚀 Starting to scrape PSX indices table")
        driver.Navigate().GoToUrl "https://dps.psx.com.pk/indices"
        logger.LogInformation("✅ Navigated to PSX indices page")
        
        // Wait for the indices table to load
        let! tableElement = WaitForElementAsync driver (By.CssSelector "table.tbl") 15.0
        
        match tableElement with
        | Error err -> 
            logger.LogError("Failed to find indices table")
            return Error err
        | Ok table ->
            // Find all rows in the table body
            let rows = table.FindElements(By.CssSelector "tbody.tbl__body tr")
            logger.LogInformation("📊 Found {Count} index rows", rows.Count)
            
            let indices = 
                rows 
                |> Seq.toList
                |> List.choose (fun row ->
                    try
                        let cells = row.FindElements(By.TagName("td"))
                        if cells.Count >= 6 then
                            // Extract index name and code from the first cell
                            let firstCell = cells.[0]
                            
                            // Try to find a clickable link first, fallback to bold text
                            let (name, code) = 
                                try
                                    let linkElement = firstCell.FindElement(By.CssSelector "a.link")
                                    let code = linkElement.GetAttribute("data-code")
                                    let name = linkElement.Text.Trim()
                                    (name, code)
                                with
                                | :? OpenQA.Selenium.NoSuchElementException ->
                                    // Fallback for rows without links (like HBLTTI)
                                    let boldElement = firstCell.FindElement(By.CssSelector "b")
                                    let name = boldElement.Text.Trim()
                                    // For non-clickable indices, use the name as code
                                    (name, name)
                                | _ ->
                                    // Last resort: just get the text content
                                    let name = firstCell.Text.Trim()
                                    (name, name)
                            
                            // Parse numeric values
                            let highText = cells.[1].Text.Trim().Replace(",", "")
                            let lowText = cells.[2].Text.Trim().Replace(",", "")
                            let currentText = cells.[3].Text.Trim().Replace(",", "")
                            let changeText = cells.[4].Text.Trim().Replace(",", "").Replace(" ", "")
                            let changePercentText = cells.[5].Text.Trim().Replace("%", "").Replace(" ", "")
                            
                            // Try to parse the values, handle "N/A" and other non-numeric values
                            let parseDecimal (text: string) = 
                                let cleanText = text.Replace("N/A", "").Replace(" ", "").Trim()
                                if String.IsNullOrEmpty(cleanText) then None
                                else
                                    match System.Decimal.TryParse(cleanText) with
                                    | true, value -> Some value
                                    | false, _ -> None
                            
                            let high = parseDecimal highText
                            let low = parseDecimal lowText
                            let current = parseDecimal currentText
                            let change = parseDecimal changeText
                            let changePercent = parseDecimal changePercentText
                            
                            match high, low, current, change, changePercent with
                            | Some h, Some l, Some c, Some ch, Some cp ->
                                Some {
                                    Name = name
                                    Code = code
                                    Current = c
                                    Change = ch
                                    ChangePercent = cp
                                    High = h
                                    Low = l
                                }
                            | _ ->
                                logger.LogWarning("❌ Could not parse values for index {Name}", name)
                                None
                        else
                            logger.LogWarning("❌ Row has insufficient cells: {CellCount}", cells.Count)
                            None
                    with
                    | ex ->
                        logger.LogError(ex, "❌ Error processing index row")
                        None)
            
            logger.LogInformation("✅ Successfully extracted {Count} indices", List.length indices)
            return Ok indices
    }
    
    try
        let! result = Retry.withPageRetry (fun () -> operation)
        return result
    with
    | ex -> 
        logger.LogError(ex, "Error scraping indices table after retries")
        return Error (DataExtractionError $"Failed to scrape indices table: {ex.Message}")
}

// Restored version with database operations
let ConstructIndexCache (forceRefresh: bool) (indexCode: string) = async {
    let logger: ILogger = LoggerContext.GetLoggerByName "Scraper"
    
    let operation = async {
        logger.LogInformation("🚀 Starting ConstructIndexCache for {IndexCode} (forceRefresh: {ForceRefresh})", indexCode, forceRefresh)
        use driver = CreateChromeDriver ()
        
        let url = "https://dps.psx.com.pk/indices"
        logger.LogInformation("🎯 Target URL: {Url}, Index Code: {Code}", url, indexCode)
        
        let! result = ScrapeIndexDataAsync driver url indexCode
        
        match result with
        | Ok (stockData: ScrapedIndexData list) ->
            logger.LogInformation("✅ Index scraping completed successfully with {Count} records", List.length stockData)
            
            // Convert to IndexTableRecord list
            let indexRecords = 
                stockData 
                |> List.map (fun data -> 
                    { Symbol = data.Symbol; Weight = data.Weight })
            
            // Convert to StockRecord list from index data
            let indexStockRecords = 
                stockData 
                |> List.map (fun data -> 
                    { Symbol = data.Symbol; Name = data.Name; Price = data.Price })
            
            // Get current portfolio to find stocks not in index
            let! currentPortfolio = getCurrentPortfolioAsync()
            logger.LogDebug("📊 Found {Count} stocks in current portfolio", List.length currentPortfolio)
            
            // Find stocks in portfolio but not in index
            let indexSymbols = Set.ofList (stockData |> List.map _.Symbol )
            let portfolioOnlySymbols = 
                currentPortfolio 
                |> List.filter (fun p -> not (Set.contains p.Symbol indexSymbols))
                |> List.map (fun p -> p.Symbol)
            
            logger.LogDebug("🔍 Found {Count} stocks in portfolio but not in index: {Symbols}", 
                List.length portfolioOnlySymbols, String.concat ", " portfolioOnlySymbols)
            
            // Fetch individual prices for portfolio-only stocks
            let! portfolioStockRecords = 
                if List.isEmpty portfolioOnlySymbols then
                    async { return [] }
                else
                    async {
                        logger.LogInformation("🌐 Fetching individual prices for {Count} portfolio-only stocks", List.length portfolioOnlySymbols)
                        let mutable results = []
                        for symbol in portfolioOnlySymbols do
                            let! priceResult = GetStockFromPSXAsync driver symbol
                            match priceResult with
                            | Ok (price, name) ->
                                let record = { Symbol = symbol; Name = name; Price = price }
                                results <- record :: results
                                logger.LogDebug("✅ Fetched price for {Symbol}: {Price}", symbol, formatPKR price)
                            | Error err ->
                                logger.LogWarning("❌ Failed to fetch price for {Symbol}: {Error}", symbol, err.ToString())
                        return results
                    }
            
            // Combine all stock records
            let allStockRecords = indexStockRecords @ portfolioStockRecords
            
            logger.LogDebug("💾 Saving {IndexCount} index records and {StockCount} stock records to database", 
                List.length indexRecords, List.length allStockRecords)
            
            // Save to database only if we have records
            if List.isEmpty indexRecords && List.isEmpty allStockRecords then
                logger.LogWarning("⚠️ No data to save - both index and stock records are empty")
            else
                if not (List.isEmpty indexRecords) then
                    do! setIndexRecordsAsync indexRecords
                if not (List.isEmpty allStockRecords) then
                    do! setStockRecordsAsync allStockRecords
            
            logger.LogInformation("✅ Cache constructed successfully with {TotalStocks} total stocks", List.length allStockRecords)
            return Ok ()
        | Error err -> 
            logger.LogError("❌ Scraping failed: {Error}", err.ToString())
            return Error err
    }
    
    try
        let! result = Retry.withCriticalRetry (fun () -> operation)
        return result
    with
    | ex -> 
        logger.LogError(ex, "❌ Unexpected error in ConstructIndexCache after retries")
        return Error (NavigationError ex.Message)
}