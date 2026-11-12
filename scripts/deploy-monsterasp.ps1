param(
  [Parameter(Mandatory = $true)][string]$PublishDirectory
)
$ErrorActionPreference = 'Stop'
$site = $env:WEBSITE_NAME
$server = $env:SERVER_COMPUTER_NAME
$login = $env:SERVER_USERNAME
$password = $env:SERVER_PASSWORD

foreach ($key in @('WEBSITE_NAME','SERVER_COMPUTER_NAME','SERVER_USERNAME','SERVER_PASSWORD')) {
  if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($key))) {
    throw "Missing required GitHub repository secret: $key"
  }
}
if ($site -notmatch '^site[A-Za-z0-9-]+$') {
  throw 'Invalid MonsterASP site ID; expected siteXXXX.'
}
if (-not (Test-Path -LiteralPath $PublishDirectory -PathType Container)) {
  throw 'Compiled publish directory does not exist.'
}
foreach ($required in @('SmartCampus.Api.dll','web.config','wwwroot/index.html','smartcampus.defaults.json')) {
  if (-not (Test-Path -LiteralPath (Join-Path $PublishDirectory $required) -PathType Leaf)) {
    throw "Publish package incomplete: $required"
  }
}
if ((Get-Content -LiteralPath (Join-Path $PublishDirectory 'web.config') -Raw) -notmatch 'AspNetCoreModuleV2') {
  throw 'ASP.NET Core IIS entrypoint is invalid.'
}
# This deployed configuration is committed to Git. Refuse secret-bearing key names.
$defaults = Get-Content -LiteralPath (Join-Path $PublishDirectory 'smartcampus.defaults.json') -Raw | ConvertFrom-Json -AsHashtable
function Assert-NonSecretDefaults($node, [string]$prefix = '') {
  foreach ($entry in $node.GetEnumerator()) {
    $path = if ($prefix) { "$($prefix):$($entry.Key)" } else { [string]$entry.Key }
    if ($path -match '(?i)(password|secret|token|credential|certificate|connectionstrings|bootstrap|universityapproval|allowedemails|api.?key|workerkey|storage:key|resendkey|brevokey|nvidiakey|openrouterkey)') {
      throw "Sensitive configuration key is forbidden in committed defaults: $path"
    }
    if ($entry.Value -is [System.Collections.IDictionary]) { Assert-NonSecretDefaults $entry.Value $path }
  }
}
Assert-NonSecretDefaults $defaults
$unsafe = @(Get-ChildItem -LiteralPath $PublishDirectory -File -Recurse |
  Where-Object { $_.Name -in @('appsettings.Local.json','appsettings.Production.json','.env','.publishSettings') -or
                 $_.Extension -in @('.pfx','.key','.pem') })
if ($unsafe.Count -gt 0) { throw 'Publish package contains a production config or private secret file.' }

$serverUrl = $server.Trim()
if ($serverUrl -notmatch '^https?://') { $serverUrl = "https://$serverUrl" }
$parsed = $null
if (-not [Uri]::TryCreate($serverUrl,[UriKind]::Absolute,[ref]$parsed) -or
    $parsed.Scheme -ne 'https' -or
    $parsed.Host -notmatch '^[A-Za-z0-9-]+\.siteasp\.net$' -or
    $parsed.Port -notin @(443,8172)) {
  throw 'Publisher must be the MonsterASP HTTPS siteXXXX.siteasp.net host.'
}
$endpoint = "https://$($parsed.Host):8172/msdeploy.axd?site=$site"
$msdeploy = 'C:\Program Files (x86)\IIS\Microsoft Web Deploy V3\msdeploy.exe'
if (-not (Test-Path -LiteralPath $msdeploy)) {
  choco install webdeploy --yes --no-progress --limit-output
  if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $msdeploy)) {
    throw 'Unable to install Microsoft Web Deploy.'
  }
}

# Check the live API before replacing any files.
function Test-CurrentApi {
  $client = [System.Net.Http.HttpClient]::new()
  $client.Timeout = [TimeSpan]::FromSeconds(20)
  try {
    $response = $client.GetAsync('https://smartcampus.tryasp.net/health/live').GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) { throw 'Public HTTPS API liveness probe failed.' }
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if ($body -notmatch '"status"\s*:\s*"live"') {
      throw 'Public endpoint did not return the expected SmartCampus API response.'
    }
  } finally {
    $client.Dispose()
  }
}
Test-CurrentApi

# Pass arguments without a shell to avoid command-line quoting issues.
# Do not print the msdeploy command, output, or exceptions: they can contain credentials.
function Invoke-WebDeploySync([bool]$PreviewOnly) {
  $argsList = @(
    '-verb:sync',
    "-source:contentPath=$PublishDirectory",
    "-dest:contentPath=$site,computerName=$endpoint,userName=$login,password=$password,authType=Basic",
    '-enableRule:DoNotDeleteRule',
    '-skip:objectName=filePath,absolutePath=appsettings([.][^\\/]+)?[.]json$',
    '-skip:objectName=filePath,absolutePath=web[.]config$',
    '-skip:objectName=dirPath,absolutePath=(App_Data|uploads|storage|DataProtection)([\\/]|$)'
  )
  if ($PreviewOnly) {
    $argsList += '-whatif'
  } else {
    # Web Deploy removes its temporary app_offline.htm after the sync.
    $argsList += '-enableRule:AppOffline'
  }
  $start = [System.Diagnostics.ProcessStartInfo]::new($msdeploy)
  $start.UseShellExecute = $false
  $start.CreateNoWindow = $true
  $start.RedirectStandardOutput = $true
  $start.RedirectStandardError = $true
  foreach ($argument in $argsList) { [void]$start.ArgumentList.Add($argument) }
  $process = [System.Diagnostics.Process]::Start($start)
  try {
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(480000)) {
      $process.Kill()
      throw 'Web Deploy timed out; check the hosting console before retrying.'
    }
    $null = $stdout.GetAwaiter().GetResult()
    $null = $stderr.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0) {
      throw 'Web Deploy failed. Sensitive provider output was intentionally withheld; verify credentials, port 8172 and remote file permissions.'
    }
  } finally {
    $process.Dispose()
  }
}
Invoke-WebDeploySync $true
Write-Output 'WebDeploy dry-run passed; existing site configuration and data will not be deleted.'
Invoke-WebDeploySync $false
Write-Output 'Compiled SmartCampus application uploaded to MonsterASP.'
Start-Sleep -Seconds 8
Test-CurrentApi
Write-Output 'Post-deployment public HTTPS API liveness probe passed.'
