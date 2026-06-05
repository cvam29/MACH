#requires -Version 7.0
<#
.SYNOPSIS
    One-command local run for the MACH composable-commerce demo.

.DESCRIPTION
    Orchestrates the full local stack with no Azure cost:

      1. Docker dependencies  — Azurite (storage), SQL Server 2022 (app DB + Service Bus
         backing store), and the Azure Service Bus emulator (topics from local/servicebus/config.json).
      2. Database             — applies EF Core migrations to create MachDb.
      3. Function hosts        — launches all seven isolated-worker hosts (ports 7070-7076),
         each in its own window, wired to the local emulators.
      4. Storefront            — `next dev` on http://localhost:3000.

    Local-infra connection strings (Azurite, the Service Bus emulator, the SQL container) are
    merged into each host's gitignored local.settings.json; any vendor secrets already present
    (commercetools / Adyen / Algolia / Contentstack keys) are preserved untouched. Offline
    providers (Maps=Stub, Cache=InMemory, Email=DevSink) mean the demo runs without real vendor
    keys — those just light up the corresponding features when supplied.

.PARAMETER Offline
    Skip Docker entirely: in-memory message bus, LocalDB, and only the synchronous hosts
    (Auth + BFF) plus the storefront. Async fan-out (Projection/Indexer/Notifications) needs the
    Service Bus emulator, so it is not started in this mode.

.PARAMETER SkipInfra      Assume Docker dependencies are already running; do not `docker compose up`.
.PARAMETER SkipMigrations Do not run `dotnet ef database update`.
.PARAMETER SkipStorefront Do not start `next dev`.
.PARAMETER Stop           Tear everything down: `docker compose down` and stop launched hosts.

.EXAMPLE
    ./run.ps1
.EXAMPLE
    ./run.ps1 -Offline          # synchronous browse path only, no Docker
.EXAMPLE
    ./run.ps1 -Stop
#>
[CmdletBinding()]
param(
    [switch]$Offline,
    [switch]$SkipInfra,
    [switch]$SkipMigrations,
    [switch]$SkipStorefront,
    [switch]$Stop
)

$ErrorActionPreference = 'Stop'
$repo = $PSScriptRoot
$mailDir = Join-Path $repo 'mail'

# --- Deterministic local connection strings ---------------------------------------------------
$saPassword = 'Mach_local_Dev123!'
$sqlContainer = "Server=localhost,1433;Database=MachDb;User Id=sa;Password=$saPassword;TrustServerCertificate=True;Encrypt=False"
$sqlLocalDb = 'Server=(localdb)\MSSQLLocalDB;Database=MachDb;Trusted_Connection=True;MultipleActiveResultSets=true'
# Canonical Service Bus emulator connection string (app on host, emulator container on localhost).
$sbConnection = 'Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;'

$sqlConnection = if ($Offline) { $sqlLocalDb } else { $sqlContainer }

# host name -> port. Auth + BFF are HTTP (sync); the rest are Service Bus / Timer workers.
$hosts = [ordered]@{
    'Mach.Auth.Functions'          = 7070
    'Mach.Bff.Functions'           = 7071
    'Mach.Webhooks.Functions'      = 7072
    'Mach.Projection.Functions'    = 7073
    'Mach.Indexer.Functions'       = 7074
    'Mach.Notifications.Functions' = 7075
    'Mach.Outbox.Functions'        = 7076
}
$offlineHosts = @('Mach.Auth.Functions', 'Mach.Bff.Functions')

function Write-Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Test-Tool($name) { if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { throw "Required tool '$name' not found on PATH." } }

# ----------------------------------------------------------------------------------------------
if ($Stop) {
    Write-Step 'Stopping launched Function hosts and storefront'
    # func/node run from their global install dirs, so match on the command line, not the image path:
    # every host is launched by this repo's working directory, and the storefront runs `next dev`.
    Get-CimInstance Win32_Process -Filter "Name='func.exe' OR Name='node.exe' OR Name='dotnet.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and ($_.CommandLine -like "*$repo*" -or $_.CommandLine -like '*func*start*' -or $_.CommandLine -like '*next*dev*') } |
        ForEach-Object { Write-Host "  stopping PID $($_.ProcessId) ($($_.Name))"; Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    if (-not $SkipInfra) {
        Write-Step 'Stopping Docker dependencies'
        docker compose -f (Join-Path $repo 'docker-compose.yml') down
    }
    Write-Host "`nStopped." -ForegroundColor Green
    return
}

# --- Prerequisite checks ----------------------------------------------------------------------
Write-Step 'Checking prerequisites'
Test-Tool dotnet
Test-Tool func
if (-not $SkipStorefront) { Test-Tool npm }
if (-not $Offline -and -not $SkipInfra) { Test-Tool docker }
Write-Host '  ok'

# --- Docker dependencies ----------------------------------------------------------------------
if (-not $Offline -and -not $SkipInfra) {
    Write-Step 'Starting Docker dependencies (Azurite, SQL Server, Service Bus emulator)'
    Write-Host '  Running this accepts the Microsoft Software License Terms for the' -ForegroundColor Yellow
    Write-Host '  Service Bus emulator and SQL Server Linux (ACCEPT_EULA=Y). See docs/run-local.md.' -ForegroundColor Yellow
    $env:ACCEPT_EULA = 'Y'
    $env:MSSQL_SA_PASSWORD = $saPassword
    docker compose -f (Join-Path $repo 'docker-compose.yml') up -d

    Write-Step 'Waiting for the Service Bus emulator health endpoint (http://localhost:5300/health)'
    $deadline = (Get-Date).AddMinutes(3)
    do {
        Start-Sleep -Seconds 3
        try { $ok = (Invoke-WebRequest 'http://localhost:5300/health' -UseBasicParsing -TimeoutSec 4).StatusCode -eq 200 }
        catch { $ok = $false }
        if (-not $ok) { Write-Host '  waiting...' }
    } until ($ok -or (Get-Date) -gt $deadline)
    if (-not $ok) { throw 'Service Bus emulator did not become healthy within 3 minutes. Check `docker compose logs`.' }
    Write-Host '  emulator healthy' -ForegroundColor Green
}

# --- Database migrations ----------------------------------------------------------------------
if (-not $SkipMigrations) {
    Write-Step 'Applying EF Core migrations (MachDb)'
    $env:SQL_CONNECTION_STRING = $sqlConnection
    dotnet ef database update --project (Join-Path $repo 'src/Mach.Persistence') | Out-Host
    Write-Host '  database up to date' -ForegroundColor Green
}

# --- Merge local-infra settings into each host's local.settings.json ---------------------------
function Merge-LocalSettings($hostName) {
    $path = Join-Path $repo "src/$hostName/local.settings.json"
    $json = if (Test-Path $path) { Get-Content $path -Raw | ConvertFrom-Json -AsHashtable } else { @{ IsEncrypted = $false; Values = @{} } }
    if (-not $json.ContainsKey('Values')) { $json['Values'] = @{} }
    $v = $json['Values']

    $infra = @{
        'AzureWebJobsStorage'       = 'UseDevelopmentStorage=true'
        'FUNCTIONS_WORKER_RUNTIME'  = 'dotnet-isolated'
        'ConnectionStrings:Sql'     = $sqlConnection
        'Email:Provider'            = 'DevSink'
        'Email:SinkDirectory'       = $mailDir
        'Cache:Provider'            = 'InMemory'
        'Maps:Provider'             = 'Stub'
    }
    if ($Offline) {
        $infra['Messaging:Provider'] = 'InMemory'
    } else {
        $infra['Messaging:Provider']         = 'ServiceBus'
        $infra['Messaging:ConnectionString'] = $sbConnection
        $infra['ServiceBusConnection']       = $sbConnection
    }
    foreach ($k in $infra.Keys) { $v[$k] = $infra[$k] }

    ($json | ConvertTo-Json -Depth 10) | Set-Content -Path $path -Encoding utf8
}

# --- Launch hosts -----------------------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $mailDir | Out-Null
$toStart = if ($Offline) { $offlineHosts } else { @($hosts.Keys) }

Write-Step "Launching $($toStart.Count) Function host(s)"
foreach ($hostName in $toStart) {
    $port = $hosts[$hostName]
    Merge-LocalSettings $hostName
    $dir = Join-Path $repo "src/$hostName"
    Write-Host "  $hostName -> http://localhost:$port"
    Start-Process -FilePath 'pwsh' -WorkingDirectory $dir -ArgumentList @(
        '-NoExit', '-Command',
        "`$host.UI.RawUI.WindowTitle = '$hostName ($port)'; func start --port $port"
    ) | Out-Null
}

# --- Storefront -------------------------------------------------------------------------------
if (-not $SkipStorefront) {
    Write-Step 'Launching storefront (next dev -> http://localhost:3000)'
    $sf = Join-Path $repo 'apps/storefront'
    if (-not (Test-Path (Join-Path $sf 'node_modules'))) {
        Write-Host '  installing storefront dependencies (npm install)...'
        Push-Location $sf; npm install | Out-Host; Pop-Location
    }
    Start-Process -FilePath 'pwsh' -WorkingDirectory $sf -ArgumentList @(
        '-NoExit', '-Command', "`$host.UI.RawUI.WindowTitle = 'storefront (3000)'; npm run dev"
    ) | Out-Null
}

# --- Summary ----------------------------------------------------------------------------------
Write-Step 'Up'
Write-Host 'Storefront    http://localhost:3000'
Write-Host 'Auth API      http://localhost:7070/api/auth'
Write-Host 'BFF API       http://localhost:7071/api'
if (-not $Offline) {
    Write-Host 'Webhooks      http://localhost:7072/api/hooks/{adyen|commercetools|contentstack}'
    Write-Host 'SB emulator   amqp://localhost:5672  (health: http://localhost:5300/health)'
    Write-Host "Email sink    $mailDir  (.eml files land here)"
}
Write-Host "`nStop everything with: ./run.ps1 -Stop" -ForegroundColor DarkGray
