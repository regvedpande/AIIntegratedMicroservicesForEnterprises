# Starts all 6 microservices in separate windows.
# Run this after stopping the old instances.

$root = $PSScriptRoot

$services = @(
    @{ Name = "Gateway";               Dir = "src\AiEnterprise.Gateway";               Port = 5000 },
    @{ Name = "ComplianceService";     Dir = "src\AiEnterprise.ComplianceService";     Port = 5001 },
    @{ Name = "DocumentIntelligence";  Dir = "src\AiEnterprise.DocumentIntelligence";  Port = 5002 },
    @{ Name = "RiskScoring";           Dir = "src\AiEnterprise.RiskScoring";           Port = 5003 },
    @{ Name = "AuditService";          Dir = "src\AiEnterprise.AuditService";          Port = 5004 },
    @{ Name = "NotificationHub";       Dir = "src\AiEnterprise.NotificationHub";       Port = 5005 }
)

foreach ($svc in $services) {
    $path = Join-Path $root $svc.Dir
    Write-Host "Building $($svc.Name)..." -ForegroundColor DarkCyan
    Push-Location $path
    dotnet build /p:UseSharedCompilation=false
    Pop-Location

    Write-Host "Starting $($svc.Name) on :$($svc.Port)..." -ForegroundColor Cyan
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$path'; `$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet run --no-build /p:UseSharedCompilation=false"
    Start-Sleep -Seconds 2
}

Write-Host ""
Write-Host "All services starting. Give them ~10s to initialize, then refresh the React app." -ForegroundColor Green
