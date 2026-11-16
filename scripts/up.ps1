$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function New-Secret {
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }

    return -join ($bytes | ForEach-Object { $_.ToString('x2') })
}

$values = [ordered]@{
    POSTGRES_DB              = 'fridmakweb'
    POSTGRES_USER            = 'fridmak'
    POSTGRES_PASSWORD        = New-Secret
    ADMIN_USERNAME           = 'admin'
    ADMIN_EMAIL              = 'admin@example.com'
    ADMIN_PASSWORD           = New-Secret
    WASMGATE_ADMIN_TOKEN     = New-Secret
    METRICS_TOKEN            = New-Secret
    GRAFANA_ADMIN_USER       = 'admin'
    GRAFANA_ADMIN_PASSWORD   = New-Secret
}

$envPath = Join-Path $root '.env'
if (-not (Test-Path $envPath)) {
    $existingVolume = docker volume ls --quiet --filter name=fridmakweb_postgres-data
    if ($existingVolume -eq 'fridmakweb_postgres-data') {
        throw 'Existing PostgreSQL volume found without .env. Restore .env or run "docker compose down -v" first.'
    }

    $content = ($values.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join [Environment]::NewLine
    [System.IO.File]::WriteAllText(
        $envPath,
        $content + [Environment]::NewLine,
        (New-Object System.Text.UTF8Encoding($false)))
    Write-Host 'Created .env with generated secrets.'
}
else {
    $content = Get-Content $envPath -Raw
    $append = @()
    foreach ($key in $values.Keys) {
        if ($content -notmatch "(?m)^$key=") {
            $append += "$key=$($values[$key])"
        }
    }

    if ($append.Count -gt 0) {
        [System.IO.File]::AppendAllText(
            $envPath,
            (($append -join [Environment]::NewLine) + [Environment]::NewLine),
            (New-Object System.Text.UTF8Encoding($false)))
        Write-Host 'Added missing values to .env.'
    }
    else {
        Write-Host 'Using existing .env.'
    }
}

$compose = Start-Process `
    -FilePath "docker" `
    -ArgumentList @("compose", "up", "-d", "--build") `
    -NoNewWindow `
    -Wait `
    -PassThru

if ($compose.ExitCode -ne 0) {
    throw "docker compose failed with exit code $($compose.ExitCode)"
}

function Read-EnvValue([string]$key) {
    return (Get-Content $envPath | Where-Object { $_ -like "$key=*" } | Select-Object -First 1) -replace "^$key=", ''
}

$wasmGateToken = Read-EnvValue 'WASMGATE_ADMIN_TOKEN'
$metricsToken = Read-EnvValue 'METRICS_TOKEN'
$grafanaUser = Read-EnvValue 'GRAFANA_ADMIN_USER'
$grafanaPassword = Read-EnvValue 'GRAFANA_ADMIN_PASSWORD'

Write-Host ''
Write-Host 'FridmakWeb stack is up:'
Write-Host '  Application (WasmGate) : http://localhost:8081'
Write-Host '  Application (direct)   : http://localhost:8082'
Write-Host '  Grafana                : http://localhost:3000'
Write-Host '  Prometheus             : http://localhost:9091'
Write-Host '  WasmGate admin         : http://localhost:19099'
Write-Host '  PostgreSQL             : localhost:5434'
Write-Host "  Grafana login          : $grafanaUser / $grafanaPassword"
Write-Host "  WasmGate admin token   : $wasmGateToken"
Write-Host "  App metrics token      : $metricsToken"
