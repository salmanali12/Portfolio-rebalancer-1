# Portfolio Import Guide

## 📥 Complete Guide to Importing Portfolio Data

This guide provides comprehensive instructions for importing your current portfolio data into the Portfolio Rebalancer application.

## 🎯 Quick Start

1. **Prepare your portfolio JSON file**
2. **Place it anywhere on your system (you'll provide the path)**
3. **Run the application and choose Option 2**
4. **Verify the import with Option 1**

## 📋 JSON Format Requirements

### Required Structure

Your portfolio JSON file must be an array of objects with exactly these fields:

```json
[
    {
        "Symbol": "STOCK_SYMBOL",
        "Shares": NUMBER_OF_SHARES,
        "Price": PRICE_PER_SHARE
    }
]
```

### ⚠️ Important: ETF Filtering

**The importer automatically excludes ETFs from your portfolio.** Only individual stocks will be imported.

**Excluded Securities:**
- ETFs (symbols ending with ETF, ETN, ETP, FUND, TRUST)
- Bonds, mutual funds, and other non-stock securities
- Any security with ETF-like naming patterns

**Example:**
- ✅ `AAPL` - Will be imported (individual stock)
- ❌ `SPY` - Will be excluded (ETF)
- ❌ `VTI` - Will be excluded (ETF)
- ❌ `QQQETF` - Will be excluded (ETF)

### Field Specifications

**Symbol** (string) - ✅ Required
- Stock ticker/symbol
- Example: `"BAHL"`, `"DCR"`

**Shares** (integer) - ✅ Required
- Number of shares owned
- Example: `650`, `3518`

**Price** (decimal) - ✅ Required
- Current price per share
- Example: `157.85`, `26.33`

### Example Portfolio

```json
[
    {
        "Symbol": "BAHL",
        "Shares": 650,
        "Price": 157.85
    },
    {
        "Symbol": "DCR",
        "Shares": 3518,
        "Price": 26.33
    },
    {
        "Symbol": "MEBL",
        "Shares": 500,
        "Price": 309.89
    },
    {
        "Symbol": "SAZEW",
        "Shares": 55,
        "Price": 1296.72
    },
    {
        "Symbol": "SYS",
        "Shares": 35,
        "Price": 119.21
    },
    {
        "Symbol": "UBL",
        "Shares": 400,
        "Price": 256.99
    }
]
```

## 🔄 Import Process

### Step 1: Prepare Your Data

1. **Export your portfolio** from your broker or financial platform
2. **Convert to JSON format** if needed
3. **Ensure all required fields** are present
4. **Validate data accuracy** (shares, prices, symbols)

### Step 2: File Preparation

```bash
# Create your portfolio JSON file
# You can place it anywhere on your system
# The application will prompt you for the file path
```

### Step 3: Import via Application

1. **Start the application:**
   ```bash
   dotnet run --project PortfolioRebalancer/
   ```

2. **Choose Option 2** from the menu:
   ```
   ┌─────────────────────────────────────────────────────────┐
   │                    Portfolio Rebalancer                 │
   ├─────────────────────────────────────────────────────────┤
   │ Option │ Description                                    │
   ├────────┼────────────────────────────────────────────────┤
   │ 2      │ Import Current Portfolio from JSON            │
   └─────────────────────────────────────────────────────────┘
   ```

3. **Enter the file path** when prompted:
   ```
   Enter the path to your portfolio JSON file: C:\Users\YourName\Documents\portfolio.json
   ```

4. **Wait for confirmation:**
   ```
   [INFO] ETFs detected and will be excluded from import:
     - SPY (ETF)
     - VTI (ETF)
   
   [SUCCESS] Current portfolio imported and saved to database.
     - File: C:\Users\YourName\Documents\portfolio.json
     - Stocks imported: 4
     - ETFs excluded: 2
     - Total value: 125000.50
   ```

### Step 4: Verify Import

1. **Choose Option 1** to view your portfolio
2. **Review the data** for accuracy
3. **Check symbol consistency** with index data

## ⚠️ Common Errors & Solutions

### File Not Found
```
[ERROR] File not found: C:\path\to\your\file.json
Please ensure the file exists and the path is correct.
```

**Solutions:**
- Check the file path spelling and case sensitivity
- Ensure the file exists at the specified location
- Use absolute paths for better reliability
- Verify file permissions and access rights

### Invalid JSON Format
```
[ERROR] Invalid JSON format: Unexpected character at position 15
Please check the JSON syntax and structure.
```

**Solutions:**
- Validate JSON syntax using online tools (jsonlint.com)
- Check for missing commas, brackets, or quotes
- Ensure proper array structure
- Look for special characters or encoding issues

### Missing Fields
```
[ERROR] Could not parse current portfolio JSON.
Please check the JSON format and ensure it matches the required structure.
```

**Solutions:**
- Ensure each record has `Symbol`, `Shares`, and `Price` fields
- Check field name spelling (case-sensitive)
- Verify no extra or missing fields
- Ensure proper JSON object structure

### Invalid Data Types
```
[ERROR] Validation errors found:
  - Record 2: Shares must be greater than 0 (got -5)
  - Record 4: Price must be greater than 0 (got 0)
```

**Solutions:**
- Ensure `Shares` is a positive whole number (integer)
- Ensure `Price` is a positive decimal number
- Remove any text formatting or currency symbols
- Check for negative values or zero values

### ETF Exclusion
```
[INFO] ETFs detected and will be excluded from import:
  - SPY (ETF)
  - VTI (ETF)
```

**Note:** This is informational only. ETFs are automatically filtered out during import.

## 🛠️ Data Validation

The import process validates:

- ✅ **File Existence**: Checks if the file exists at the specified path
- ✅ **File Size**: Ensures the file is not empty
- ✅ **JSON Structure**: Validates proper JSON format
- ✅ **Required Fields**: Verifies all required fields are present
- ✅ **Data Types**: Ensures correct types for each field
- ✅ **Business Rules**: Validates shares > 0, prices > 0, non-empty symbols
- ✅ **ETF Filtering**: Automatically excludes ETFs and non-stock securities
- ✅ **Database Storage**: Successfully saves to PostgreSQL
- ✅ **Cache Invalidation**: Clears old portfolio cache

## 📊 Sample Data Usage

### Creating Sample Data

```bash
# Create a sample portfolio file using the format shown above
# Example: sample-portfolio.json
# Then run the application and import using Option 2
```

### Sample Portfolio Example

Here's an example of what your portfolio JSON should look like:
- **Individual stocks** (not ETFs)
- **Realistic share counts** and prices
- **Proper JSON formatting**

## 🔧 Advanced Usage

### Multiple Portfolios

Work with different portfolios by providing different file paths:

```bash
# Import different portfolios
# When prompted, enter the path to each portfolio file
# Example paths:
# C:\Portfolios\portfolio-a.json
# C:\Portfolios\portfolio-b.json
# ./data/my-portfolio.json
# /home/user/portfolios/retirement.json
```

### Automated Import Script

```bash
#!/bin/bash
# portfolio-import.sh
PORTFOLIO_FILE="$1"

if [ -z "$PORTFOLIO_FILE" ]; then
    echo "Usage: $0 <portfolio-file.json>"
    exit 1
fi

echo "Portfolio file: $PORTFOLIO_FILE"
echo "Note: Manual import required - run the application and choose Option 2"
echo "Then enter the path: $PORTFOLIO_FILE"
```

### Batch Processing

```bash
# Process multiple portfolio files
for file in portfolios/*.json; do
    echo "Processing $file..."
    echo "Note: Manual import required for each file"
    echo "Run the application and choose Option 2, then enter: $file"
done
```

## 🎯 Best Practices

### Data Preparation
1. **Use current prices** - Update prices before importing
2. **Verify share counts** - Ensure accuracy of holdings
3. **Consistent symbols** - Use same symbols as index data
4. **Backup original** - Keep copy of source data
5. **Separate ETFs** - ETFs will be automatically excluded, so focus on individual stocks

### File Management
1. **File paths** - Use absolute paths for reliability
2. **File permissions** - Ensure read access to portfolio files
3. **File format** - Use UTF-8 encoding for JSON files
4. **Version control** - Track changes to portfolio data

### Validation
1. **Pre-import check** - Validate JSON before importing
2. **Post-import verify** - Use Option 1 to review data
3. **Cross-reference** - Check against index symbols
4. **Error handling** - Address any import errors

## 🔗 Integration Workflow

After successful import:

1. **Get Market Data** (Option 5)
   - Fetch current prices for all symbols
   - Update price data in database

2. **View Portfolio** (Option 1)
   - Verify imported holdings
   - Check total portfolio value

3. **Rebalance Portfolio** (Option 4)
   - Use imported data for rebalancing
   - Calculate buy/sell orders

4. **Compare Strategies** (Option 3)
   - Compare with index-based approach
   - Analyze different strategies

## 📈 Performance Considerations

- **Caching**: Imported data is cached for 5 minutes
- **Database**: Data stored in PostgreSQL for persistence
- **Validation**: Comprehensive validation prevents errors
- **Error Recovery**: Clear error messages guide troubleshooting

## 🆘 Troubleshooting Checklist

- [ ] File exists at the specified path
- [ ] File path is correct and accessible
- [ ] JSON syntax is valid
- [ ] All required fields present
- [ ] Data types are correct
- [ ] Database is running
- [ ] Application has read permissions
- [ ] No special characters in symbols
- [ ] Prices are positive decimal numbers
- [ ] Shares are positive whole numbers
- [ ] File is not empty
- [ ] File encoding is UTF-8
- [ ] Portfolio contains individual stocks (not just ETFs)
- [ ] ETF exclusion is expected behavior

## 📞 Support

If you encounter issues:

1. **Check this guide** for common solutions
2. **Review error messages** carefully
3. **Validate JSON format** using online tools
4. **Test with sample data** first
5. **Check application logs** for details

---

**Happy Portfolio Importing! 📥📊** 