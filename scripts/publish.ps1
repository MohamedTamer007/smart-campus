param([string]$Destination = 'work/publish')
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
npm ci
if ($LASTEXITCODE -ne 0) { throw 'npm ci failed' }
npm run build
if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed' }
dotnet publish backend/SmartCampus.Api -c Release -o $Destination
if ($LASTEXITCODE -ne 0) { throw 'Backend publish failed' }
$webRoot = Join-Path $Destination 'wwwroot'
New-Item -ItemType Directory -Force -Path $webRoot | Out-Null
Copy-Item -Path dist/* -Destination $webRoot -Recurse -Force
if (Test-Path (Join-Path $Destination 'appsettings.Local.json')) { throw 'Local configuration must never be published' }
