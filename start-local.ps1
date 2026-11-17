$ErrorActionPreference = 'Stop'
$ApiPort = 44300
$StorePort = 5175
$projectRoot = $PSScriptRoot
$apiProject = Join-Path $projectRoot 'backend/OneCart.Api/OneCart.Api.csproj'
$frontendRoot = Join-Path $projectRoot 'frontend'
$logRoot = Join-Path $projectRoot 'backend/OneCart.Api/App_Data/logs'

function Test-ListeningPort([int]$Port) {
    return [bool](Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
}

function Wait-ForUrl([string]$Url, [System.Diagnostics.Process]$Process, [int]$Attempts = 30) {
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        if ($Process.HasExited) { return $false }
        try {
            $response = Invoke-WebRequest -Uri $Url -TimeoutSec 2 -UseBasicParsing
            if ($response.StatusCode -eq 200) { return $true }
        } catch {
            # The server can still be starting.
        }
        Start-Sleep -Seconds 1
    }
    return $false
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 10 SDK is required. Install it before starting the API.'
}
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw 'Node.js and npm are required. Install them before starting the storefront.'
}
if (-not (Test-Path (Join-Path $frontendRoot 'node_modules'))) {
    throw 'Frontend packages are missing. Run npm ci in the frontend directory first.'
}
& dotnet dev-certs https --check --trust *> $null
if ($LASTEXITCODE -ne 0) {
    throw "No trusted HTTPS development certificate exists for Windows account $([System.Security.Principal.WindowsIdentity]::GetCurrent().Name). Run 'dotnet dev-certs https --trust' in this account, then retry."
}
if (Test-ListeningPort $ApiPort) {
    throw "Port $ApiPort is already in use. Stop the other API process or choose a free port."
}
if (Test-ListeningPort $StorePort) {
    throw "Port $StorePort is already in use. Stop the other storefront process or choose a free port."
}

New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = "https://localhost:$ApiPort"
$api = Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--project', $apiProject, '--no-launch-profile') -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logRoot 'local-api.out.log') -RedirectStandardError (Join-Path $logRoot 'local-api.err.log')

if (-not (Wait-ForUrl "https://localhost:$ApiPort/health" $api)) {
    if (-not $api.HasExited) { Stop-Process -Id $api.Id -ErrorAction SilentlyContinue }
    throw "The HTTPS API did not become healthy. Check $(Join-Path $logRoot 'local-api.err.log') and local-api.out.log."
}

$env:VITE_API_BASE_URL = "https://localhost:$ApiPort"
$store = Start-Process -FilePath 'npm.cmd' -ArgumentList @('run', 'dev', '--', '--host', '127.0.0.1', '--port', $StorePort, '--strictPort') -WorkingDirectory $frontendRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logRoot 'local-store.out.log') -RedirectStandardError (Join-Path $logRoot 'local-store.err.log')
if (-not (Wait-ForUrl "http://127.0.0.1:$StorePort/" $store)) {
    if (-not $store.HasExited) { Stop-Process -Id $store.Id -ErrorAction SilentlyContinue }
    if (-not $api.HasExited) { Stop-Process -Id $api.Id -ErrorAction SilentlyContinue }
    throw "The storefront did not become ready. Check $(Join-Path $logRoot 'local-store.err.log') and local-store.out.log."
}

Write-Host "API:        https://localhost:$ApiPort/health (process $($api.Id))"
Write-Host "Storefront: http://127.0.0.1:$StorePort/ (process $($store.Id))"
Write-Host "Logs:       $logRoot"
Write-Host 'Use Stop-Process with the process IDs above when finished.'
