#!/bin/bash

# Set environment to Development
export ASPNETCORE_ENVIRONMENT="Development"

echo "Environment: $ASPNETCORE_ENVIRONMENT"

# Run the application
dotnet run --project src/PortfolioRebalancer.Console/ 