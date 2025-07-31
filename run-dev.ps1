#!/usr/bin/env pwsh

# Set environment to Development
$env:ASPNETCORE_ENVIRONMENT = "Development"

Write-Host "Environment: $env:ASPNETCORE_ENVIRONMENT" -ForegroundColor Cyan

# Run the application
dotnet run --project src/PortfolioRebalancer.Console/ 