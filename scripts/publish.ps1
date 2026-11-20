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
# Record the exact source commit in the public package, without secrets.
$sourceCommit = (& git rev-parse --verify HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or $sourceCommit -notmatch '^[0-9a-f]{40}$') {
  throw 'Cannot identify the source commit for this release.'
}
$buildInfoPath = Join-Path $webRoot 'build-info.json'
@{ commit = $sourceCommit } | ConvertTo-Json -Compress |
  Set-Content -LiteralPath $buildInfoPath -Encoding utf8 -NoNewline
if (Test-Path (Join-Path $Destination 'appsettings.Local.json')) { throw 'Local configuration must never be published' }
