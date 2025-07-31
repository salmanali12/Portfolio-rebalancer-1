@echo off

REM Set environment to Development
set ASPNETCORE_ENVIRONMENT=Development

echo Environment: %ASPNETCORE_ENVIRONMENT%

REM Run the application
dotnet run --project src/PortfolioRebalancer.Console/ 