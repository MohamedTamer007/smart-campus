param([ValidateSet('Retrieval', 'Smoke')][string] $Suite = 'Retrieval')
$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$clusterPath = '/tmp/smartcampus_codex_pg_20261113'
$databaseName = 'smartcampus_codex_20261113_test'
$connection = "Host=127.0.0.1;Port=55439;Database=$databaseName;Username=mohamad"
$distro = 'Ubuntu'
$startedHere = $false
$secret = $null
$secretPointer = [IntPtr]::Zero
$plainSecret = $null
$exitCode = 1

function Invoke-Wsl([string[]] $Arguments) {
    & wsl.exe -d $distro -- @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "WSL command failed with exit code $LASTEXITCODE. No API key was written to output."
    }
}

try {
    $ready = $false
    & wsl.exe -d $distro -- pg_isready -h 127.0.0.1 -p 55439 *> $null
    $ready = $LASTEXITCODE -eq 0

    if (-not $ready) {
        Invoke-Wsl -Arguments @('/usr/lib/postgresql/16/bin/pg_ctl', '-D', $clusterPath, '-l', "$clusterPath.log", '-o', '-p 55439 -h 127.0.0.1', '-w', 'start')
        $startedHere = $true
    }

    $actualDatabase = & wsl.exe -d $distro -- psql -h 127.0.0.1 -p 55439 -U mohamad -d $databaseName -Atc 'select current_database()'
    if ($LASTEXITCODE -ne 0 -or ($actualDatabase -join '').Trim() -ne $databaseName) {
        throw 'The isolated Gemini test database could not be verified; no key was requested and no test was started.'
    }

    Write-Host 'This runs only the fictional 20-request Gemini retrieval test against the local _test database.'
    Write-Host 'The key is held in process memory only and is cleared when the test exits.'
    $secret = Read-Host 'Enter the separate Gemini TEST key (input is hidden)' -AsSecureString
    if ($secret.Length -lt 10) { throw 'The entered key was too short; no API request was sent.' }

    $secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
    $plainSecret = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer)
    [Environment]::SetEnvironmentVariable('SMART_CAMPUS_GEMINI_TEST_KEY', $plainSecret, 'Process')
    [Environment]::SetEnvironmentVariable('SMART_CAMPUS_RUN_LIVE_AI_TESTS', 'true', 'Process')
    [Environment]::SetEnvironmentVariable('SMART_CAMPUS_TEST_DB', $connection, 'Process')

    Push-Location $repositoryRoot
    try {
        $testClass = if ($Suite -eq 'Smoke') { 'GeminiLiveTests' } else { 'GeminiRetrievalLiveTests' }
        $resultFile = if ($Suite -eq 'Smoke') { 'gemini-smoke-live-20261114.trx' } else { 'gemini-retrieval-live-20261114.trx' }
        & dotnet test backend/SmartCampus.Tests/SmartCampus.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~$testClass" --logger "trx;LogFileName=$resultFile"
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    $exitCode = 1
}
finally {
    [Environment]::SetEnvironmentVariable('SMART_CAMPUS_GEMINI_TEST_KEY', $null, 'Process')
    [Environment]::SetEnvironmentVariable('SMART_CAMPUS_RUN_LIVE_AI_TESTS', $null, 'Process')
    [Environment]::SetEnvironmentVariable('SMART_CAMPUS_TEST_DB', $null, 'Process')
    $plainSecret = $null
    if ($secretPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer)
    }
    if ($secret) { $secret.Dispose() }
    if ($startedHere) {
        & wsl.exe -d $distro -- /usr/lib/postgresql/16/bin/pg_ctl -D $clusterPath -m fast -w stop *> $null
    }
}

exit $exitCode
