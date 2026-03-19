<#
.SYNOPSIS
    Sets up dotnet user-secrets for all AiEnterprise ECRI services.

.DESCRIPTION
    Run this script once after cloning to configure all required secrets
    for local development. Secrets are stored outside the repository
    using .NET User Secrets (never committed to git).

.PARAMETER JwtKey
    JWT signing key (minimum 32 characters). All services MUST share the same key.
    Leave blank to auto-generate a secure key.

.PARAMETER SqlConnectionString
    SQL Server connection string. Defaults to LocalDB.

.PARAMETER RedisConnection
    Redis connection string. Defaults to localhost:6379.

.PARAMETER GatewayApiKey
    Gateway API key for X-Api-Key header. Leave blank to auto-generate.

.PARAMETER AnthropicApiKey
    Your Anthropic API key (get from https://console.anthropic.com).
    Required only for Document Intelligence service.

.EXAMPLE
    .\scripts\setup-secrets.ps1

.EXAMPLE
    .\scripts\setup-secrets.ps1 -AnthropicApiKey "sk-ant-..."
#>

param(
    [string]$JwtKey = "",
    [string]$SqlConnectionString = "Server=(localdb)\mssqllocaldb;Database=AiEnterpriseDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;",
    [string]$RedisConnection = "localhost:6379",
    [string]$GatewayApiKey = "",
    [string]$AnthropicApiKey = ""
)

# Auto-generate secrets if not provided
if ([string]::IsNullOrEmpty($JwtKey)) {
    $bytes = New-Object byte[] 48
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $JwtKey = [Convert]::ToBase64String($bytes)
    Write-Host "Generated JWT Key: $JwtKey" -ForegroundColor Yellow
    Write-Host "SAVE THIS KEY - all services must use the same key!" -ForegroundColor Red
}

if ([string]::IsNullOrEmpty($GatewayApiKey)) {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $GatewayApiKey = [System.BitConverter]::ToString($bytes).Replace("-", "").ToLower()
    Write-Host "Generated Gateway API Key: $GatewayApiKey" -ForegroundColor Yellow
}

if ([string]::IsNullOrEmpty($AnthropicApiKey)) {
    Write-Host "WARNING: Anthropic API Key not provided. Document Intelligence service will fail." -ForegroundColor Yellow
    Write-Host "Get your key at https://console.anthropic.com" -ForegroundColor Yellow
    $AnthropicApiKey = "REPLACE_WITH_YOUR_ANTHROPIC_API_KEY"
}

$repoRoot = Split-Path -Parent $PSScriptRoot

Write-Host "`n=== Setting up AiEnterprise ECRI secrets ===" -ForegroundColor Cyan

$services = @(
    @{
        Name = "AiEnterprise.Gateway"
        Path = Join-Path $repoRoot "src\AiEnterprise.Gateway"
        Secrets = @{
            "Jwt:Key"       = $JwtKey
            "Gateway:ApiKey" = $GatewayApiKey
            "ConnectionStrings:Redis" = $RedisConnection
        }
    },
    @{
        Name = "AiEnterprise.ComplianceService"
        Path = Join-Path $repoRoot "src\AiEnterprise.ComplianceService"
        Secrets = @{
            "Jwt:Key" = $JwtKey
            "ConnectionStrings:DefaultConnection" = $SqlConnectionString
            "ConnectionStrings:Redis" = $RedisConnection
        }
    },
    @{
        Name = "AiEnterprise.DocumentIntelligence"
        Path = Join-Path $repoRoot "src\AiEnterprise.DocumentIntelligence"
        Secrets = @{
            "Jwt:Key" = $JwtKey
            "Anthropic:ApiKey" = $AnthropicApiKey
            "ConnectionStrings:DefaultConnection" = $SqlConnectionString
            "ConnectionStrings:Redis" = $RedisConnection
        }
    },
    @{
        Name = "AiEnterprise.RiskScoring"
        Path = Join-Path $repoRoot "src\AiEnterprise.RiskScoring"
        Secrets = @{
            "Jwt:Key" = $JwtKey
            "ConnectionStrings:DefaultConnection" = $SqlConnectionString
            "ConnectionStrings:Redis" = $RedisConnection
        }
    },
    @{
        Name = "AiEnterprise.AuditService"
        Path = Join-Path $repoRoot "src\AiEnterprise.AuditService"
        Secrets = @{
            "Jwt:Key" = $JwtKey
            "ConnectionStrings:DefaultConnection" = $SqlConnectionString
            "ConnectionStrings:Redis" = $RedisConnection
        }
    },
    @{
        Name = "AiEnterprise.NotificationHub"
        Path = Join-Path $repoRoot "src\AiEnterprise.NotificationHub"
        Secrets = @{
            "Jwt:Key" = $JwtKey
            "ConnectionStrings:DefaultConnection" = $SqlConnectionString
            "ConnectionStrings:Redis" = $RedisConnection
        }
    }
)

foreach ($svc in $services) {
    Write-Host "`n  Configuring $($svc.Name)..." -ForegroundColor Cyan
    Push-Location $svc.Path

    foreach ($kvp in $svc.Secrets.GetEnumerator()) {
        $result = dotnet user-secrets set $kvp.Key $kvp.Value 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "    [OK] $($kvp.Key)" -ForegroundColor Green
        } else {
            Write-Host "    [FAIL] $($kvp.Key): $result" -ForegroundColor Red
        }
    }

    Pop-Location
}

Write-Host "`n=== Setup complete! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor White
Write-Host "  1. Start Redis: docker run -d -p 6379:6379 redis:7-alpine" -ForegroundColor White
Write-Host "  2. Create database: sqlcmd -S '(localdb)\mssqllocaldb' -Q 'CREATE DATABASE AiEnterpriseDb'" -ForegroundColor White
Write-Host "  3. Start services (in separate terminals):" -ForegroundColor White
Write-Host "     cd src/AiEnterprise.ComplianceService    && dotnet run" -ForegroundColor Gray
Write-Host "     cd src/AiEnterprise.DocumentIntelligence && dotnet run" -ForegroundColor Gray
Write-Host "     cd src/AiEnterprise.RiskScoring          && dotnet run" -ForegroundColor Gray
Write-Host "     cd src/AiEnterprise.AuditService         && dotnet run" -ForegroundColor Gray
Write-Host "     cd src/AiEnterprise.NotificationHub      && dotnet run" -ForegroundColor Gray
Write-Host "     cd src/AiEnterprise.Gateway              && dotnet run" -ForegroundColor Gray
Write-Host ""
Write-Host "  4. Open http://localhost:5000/swagger to test the API" -ForegroundColor White
Write-Host ""
Write-Host "Your Gateway API Key: $GatewayApiKey" -ForegroundColor Yellow
Write-Host "(Add this as X-Api-Key header in all API requests)" -ForegroundColor Yellow
