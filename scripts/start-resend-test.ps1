#Requires -Version 5.1
param(
    [Parameter(Mandatory = $true)][string]$AuthorizedEmail,
    [string]$DotnetPath = 'dotnet',
    [switch]$ValidateOnly
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repository 'backend/SmartCampus.Api/SmartCampus.Api.csproj'
$localSettings = Join-Path $repository 'backend/SmartCampus.Api/appsettings.Local.json'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'This interactive launcher is for Windows.' }
$mailbox = New-Object System.Net.Mail.MailAddress($AuthorizedEmail.Trim())
if ($mailbox.Address -ine $AuthorizedEmail.Trim() -or $mailbox.Host -ine 'o6u.edu.eg') { throw 'Use the exact authorized university mailbox.' }
if ((& $DotnetPath --version) -notmatch '^10\.') { throw 'Select the installed .NET 10 SDK with -DotnetPath.' }
$dotnetDirectory = Split-Path (Get-Command $DotnetPath -CommandType Application).Source -Parent
if (Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue) { throw 'Stop the existing local API before starting this single-worker mail test.' }
try {
    $connection = [Environment]::GetEnvironmentVariable('ConnectionStrings__Campus', 'Process')
    if (-not $connection) { $connection = (Get-Content -LiteralPath $localSettings -Raw | ConvertFrom-Json).ConnectionStrings.Campus }
    $database = New-Object System.Data.Common.DbConnectionStringBuilder
    $database.set_ConnectionString($connection)
    if ($database['Host'] -notin @('127.0.0.1', 'localhost') -or [string]$database['Database'] -notmatch '^[A-Za-z0-9_]+_dev$') { throw 'Unsafe test database' }
} catch { throw 'This launcher requires the existing isolated loopback development database; no hosted database will be used.' }
if ($ValidateOnly) { Write-Host 'Resend launcher preflight passed; no key requested and no email sent.'; return }

Write-Host 'Enter a Resend Sending access API key. Input is hidden and is not written to files or command arguments.'
Write-Host 'The authorized mailbox must be the email on your Resend account. Use this PC to open the verification link.'
$secureKey = Read-Host 'Resend API key' -AsSecureString
$pointer = [IntPtr]::Zero
$previous = @{}
$settings = @{}
try {
    if ($secureKey.Length -eq 0) { throw 'No API key was supplied.' }
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    $settings = @{
        DOTNET_ROOT = $dotnetDirectory
        PATH = $dotnetDirectory + ';' + [Environment]::GetEnvironmentVariable('PATH', 'Process')
        ASPNETCORE_ENVIRONMENT = 'Development'
        ConnectionStrings__Campus = $connection
        PublicOrigin = 'http://127.0.0.1:5080'
        Email__Provider = 'resend'
        Email__LocalSink = 'false'
        Email__From = 'Smart Campus testing <onboarding@resend.dev>'
        Email__TestRecipient = $mailbox.Address.ToLowerInvariant()
        Email__ResendKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
        Release__Mode = 'controlled'
        Release__AllowedEmails__0 = $mailbox.Address.ToLowerInvariant()
        AI__AllowNonSensitiveInference = 'false'
        AI__AllowNonSensitiveImages = 'false'
        Search__CalibrationApproval = ''
    }
    foreach ($name in $settings.Keys) {
        $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $settings[$name], 'Process')
    }
    Write-Host 'Starting the controlled local test at http://127.0.0.1:5080. Leave this window running; Ctrl+C stops it.'
    & $DotnetPath run --no-build --project $project --urls 'http://127.0.0.1:5080'
    if ($LASTEXITCODE -ne 0) { throw 'The local API stopped with an error. The API key was not saved.' }
} finally {
    foreach ($name in $previous.Keys) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    if ($pointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    if ($settings) { $settings.Clear() }
    $secureKey.Dispose()
}
