#r "nuget: Selenium.WebDriver"
#r "nuget: Selenium.Support"
#r "nuget: Microsoft.Extensions.Logging"
#r "nuget: Microsoft.Extensions.Logging.Console"
#r "nuget: Serilog"
#r "nuget: Serilog.Extensions.Logging"
#r "nuget: Serilog.Sinks.Console"
#r "nuget: Serilog.Sinks.File"

open System
open System.Threading
open System.Collections.Generic
open System.Collections.ObjectModel
open OpenQA.Selenium
open OpenQA.Selenium.Chrome
open OpenQA.Selenium.Support.UI
open Microsoft.Extensions.Logging
open Serilog
open Serilog.Extensions.Logging

// Setup logging
let logger = 
    LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console(outputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File("debug_scraper.log", outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .CreateLogger()

let loggerFactory = new SerilogLoggerFactory(logger)
let scraperLogger = loggerFactory.CreateLogger("DebugScraper")

let CreateChromeDriver () =
    let options = ChromeOptions()
    options.AddArgument("--headless")
    options.AddArgument("--no-sandbox")
    options.AddArgument("--disable-dev-shm-usage")
    options.AddArgument("--disable-gpu")
    options.AddArgument("--disable-extensions")
    options.AddArgument("--disable-plugins")
    options.AddArgument("--disable-images")
    options.AddArgument("--disable-web-security")
    options.AddArgument("--disable-features=VizDisplayCompositor")
    options.AddArgument("--window-size=1920,1080")
    options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36")
    options.AddArgument("--log-level=3")
    options.AddArgument("--silent")
    options.AddArgument("--disable-logging")
    options.AddArgument("--disable-blink-features=AutomationControlled")
    options.AddArgument("--disable-background-timer-throttling")
    options.AddArgument("--disable-backgrounding-occluded-windows")
    options.AddArgument("--disable-renderer-backgrounding")
    
    scraperLogger.LogInformation("Creating ChromeDriver with headless configuration...")
    let driver = new ChromeDriver(options)
    scraperLogger.LogInformation("ChromeDriver created successfully")
    driver

let LogPageState (driver: IWebDriver) (context: string) =
    try
        scraperLogger.LogDebug("=== PAGE STATE [{Context}] ===", context)
        scraperLogger.LogDebug("Current URL: {Url}", driver.Url)
        scraperLogger.LogDebug("Page Title: {Title}", driver.Title)
        scraperLogger.LogDebug("Page Source Length: {Length}", driver.PageSource.Length)
        
        // Log all links on the page for debugging
        let links = driver.FindElements(By.TagName("a"))
        scraperLogger.LogDebug("Total links found: {Count}", links.Count)
        
        // Log links with data-code attributes
        let dataCodeLinks = driver.FindElements(By.CssSelector("a[data-code]"))
        scraperLogger.LogDebug("Links with data-code attribute: {Count}", dataCodeLinks.Count)
        for link in dataCodeLinks do
            let dataCode = link.GetAttribute("data-code")
            let href = link.GetAttribute("href")
            let text = link.Text
            scraperLogger.LogDebug("  Link: data-code='{DataCode}', href='{Href}', text='{Text}'", dataCode, href, text)
        
        // Log all elements with data-code attribute
        let allDataCodeElements = driver.FindElements(By.CssSelector("[data-code]"))
        scraperLogger.LogDebug("All elements with data-code attribute: {Count}", allDataCodeElements.Count)
        for element in allDataCodeElements do
            let dataCode = element.GetAttribute("data-code")
            let tagName = element.TagName
            let text = element.Text
            scraperLogger.LogDebug("  Element: {TagName} data-code='{DataCode}', text='{Text}'", tagName, dataCode, text)
            
    with
    | ex -> scraperLogger.LogError(ex, "Error logging page state for context: {Context}", context)

let WaitForElementAsync (driver: IWebDriver) (by: By) (timeoutSeconds: float) = async {
    scraperLogger.LogDebug("🔍 Waiting for element: {Element} (timeout: {Timeout} seconds)", by, timeoutSeconds)
    
    let startTime = DateTime.Now
    let wait = WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds))
    
    try
        let element = wait.Until(fun d -> 
            let found = d.FindElement(by)
            scraperLogger.LogDebug("✅ Element found: {Element}", by)
            found
        )
        let elapsed = DateTime.Now - startTime
        scraperLogger.LogDebug("✅ Element found successfully after {Elapsed}ms: {Element}", elapsed.TotalMilliseconds, by)
        return Ok element
    with
    | ex -> 
        let elapsed = DateTime.Now - startTime
        scraperLogger.LogError(ex, "❌ Element not found after {Elapsed}ms (timeout: {Timeout}s): {Element}", elapsed.TotalMilliseconds, timeoutSeconds, by)
        
        // Log current page state when element is not found
        LogPageState driver (sprintf "ElementNotFound_%s" (by.ToString()))
        
        return Error ex.Message
}

let TestScraping () = async {
    scraperLogger.LogInformation("🚀 Starting debug scraping test...")
    
    try
        use driver = CreateChromeDriver()
        
        let url = "https://www.psx.com.pk/psx/resources-and-tools/indices/kse-mis"
        let code = "KSE30"
        
        scraperLogger.LogInformation("🎯 Testing URL: {Url}, Index Code: {Code}", url, code)
        
        // Navigate to the page
        scraperLogger.LogDebug("🌐 Navigating to URL: {Url}", url)
        driver.Navigate().GoToUrl url
        scraperLogger.LogInformation("✅ Navigated to: {Url}", url)
        
        // Log initial page state
        LogPageState driver "AfterNavigation"
        
        // Wait for page to load with retry logic
        scraperLogger.LogDebug("⏳ Waiting for page to fully load...")
        let maxRetries = 3
        let mutable retryCount = 0
        let mutable pageLoaded = false
        
        while not pageLoaded && retryCount < maxRetries do
            do! Async.Sleep 3000
            retryCount <- retryCount + 1
            scraperLogger.LogDebug("⏳ Page load attempt {Attempt}/{MaxAttempts}", retryCount, maxRetries)
            
            // Check if page has loaded by looking for any content
            try
                let bodyElement = driver.FindElement(By.TagName("body"))
                let bodyText = bodyElement.Text
                if bodyText.Length > 100 then
                    pageLoaded <- true
                    scraperLogger.LogDebug("✅ Page appears to be loaded (body text length: {Length})", bodyText.Length)
                else
                    scraperLogger.LogDebug("⏳ Page still loading (body text length: {Length})", bodyText.Length)
            with
            | ex -> 
                scraperLogger.LogDebug("⏳ Page not ready yet: {Error}", ex.Message)
        
        // Log page state after wait
        LogPageState driver "AfterWait"
        
        // Try to find the KSE link
        scraperLogger.LogDebug("🔍 Looking for KSE link with data-code='{Code}'", code)
        
        // Try multiple selectors to find the element
        let selectors = [
            $"a[data-code='{code}']"
            $"*[data-code='{code}']"
            $"a[href*='{code}']"
        ]
        
        let rec trySelectors (remainingSelectors: string list) = async {
            match remainingSelectors with
            | [] -> 
                scraperLogger.LogError("❌ None of the selectors found the element for {Code}", code)
                return Error "No element found"
            | selector :: rest ->
                scraperLogger.LogDebug("🔍 Trying selector: {Selector}", selector)
                let! result = WaitForElementAsync driver (By.CssSelector selector) 5.0
                match result with
                | Ok element ->
                    scraperLogger.LogDebug("✅ Found element with selector: {Selector}", selector)
                    return Ok element
                | Error _ ->
                    return! trySelectors rest
        }
        
        let! kseLink = trySelectors selectors
        
        match kseLink with
        | Error err -> 
            scraperLogger.LogError("❌ Failed to find KSE link for {Code}: {Error}", code, err)
            return Error err
        | Ok element ->
            scraperLogger.LogDebug("✅ Found KSE link for {Code}, attempting to click", code)
            scraperLogger.LogDebug("Link text: '{Text}', href: '{Href}'", element.Text, element.GetAttribute("href"))
            
            // Click the element
            element.Click()
            scraperLogger.LogInformation("✅ Clicked {Code} link successfully", code)
            
            // Wait for page to load after click
            scraperLogger.LogDebug("⏳ Waiting 5 seconds for page to load after click...")
            do! Async.Sleep 5000
            
            // Log page state after click
            LogPageState driver "AfterClick"
            
            scraperLogger.LogInformation("✅ Debug scraping test completed successfully")
            return Ok ()
            
    with
    | ex -> 
        scraperLogger.LogError(ex, "❌ Debug scraping test failed")
        return Error ex.Message
}

// Run the test
scraperLogger.LogInformation("🚀 Starting debug scraper test...")
let result = TestScraping() |> Async.RunSynchronously

match result with
| Ok _ -> 
    scraperLogger.LogInformation("✅ Debug test completed successfully!")
    printfn "✅ Debug test completed successfully! Check debug_scraper.log for detailed output."
| Error err -> 
    scraperLogger.LogError("❌ Debug test failed: {Error}", err)
    printfn "❌ Debug test failed: %s" err 