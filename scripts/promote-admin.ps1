param(
    [Parameter(Mandatory = $true)]
    [string]$Username
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$envPath = Join-Path $root '.env'
if (-not (Test-Path $envPath)) {
    throw '.env not found. Start the stack with ./scripts/up.ps1 first.'
}

function Read-EnvValue([string]$key, [string]$fallback) {
    $line = Get-Content $envPath | Where-Object { $_ -like "$key=*" } | Select-Object -First 1
    if (-not $line) {
        return $fallback
    }

    return ($line -replace "^$key=", '')
}

$dbUser = Read-EnvValue 'POSTGRES_USER' 'fridmak'
$dbName = Read-EnvValue 'POSTGRES_DB' 'fridmakweb'
$escapedUsername = $Username.Replace("'", "''")

$sql = @"
UPDATE "Users"
SET "Role" = 'Admin'
WHERE "Username" = '$escapedUsername';

SELECT "Id", "Username", "Role"
FROM "Users"
WHERE "Username" = '$escapedUsername';
"@

$result = $sql | docker compose exec -T db psql -U $dbUser -d $dbName
if ($LASTEXITCODE -ne 0) {
    throw "psql failed with exit code $LASTEXITCODE"
}

Write-Host $result
Write-Host ''
Write-Host "User '$Username' is now Admin. Log out and log in again to refresh the role claim."
