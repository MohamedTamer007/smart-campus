param([int]$Port = 5080)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project backend/SmartCampus.Api --urls "http://127.0.0.1:$Port"
