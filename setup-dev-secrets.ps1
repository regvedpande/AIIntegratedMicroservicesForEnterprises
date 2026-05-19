# Run this once to configure local JWT & API secrets for development.
# These values are stored in your Windows user profile (dotnet user-secrets),
# never committed to git.

$JWT_KEY     = "dev-local-testing-jwt-secret-key-32chars!!"
$GATEWAY_KEY = "dev-gateway-api-key-local-testing-only"
$NVIDIA_KEY  = "REPLACE_WITH_YOUR_NVIDIA_API_KEY"   # <-- set your real NVIDIA key here

$services = @(
    "src\AiEnterprise.Gateway",
    "src\AiEnterprise.ComplianceService",
    "src\AiEnterprise.DocumentIntelligence",
    "src\AiEnterprise.RiskScoring",
    "src\AiEnterprise.AuditService",
    "src\AiEnterprise.NotificationHub"
)

foreach ($svc in $services) {
    Write-Host "Setting secrets for $svc ..." -ForegroundColor Cyan
    Push-Location $svc
    dotnet user-secrets set "Jwt:Key" $JWT_KEY
    dotnet user-secrets set "Jwt:Issuer" "AiEnterpriseGateway"
    dotnet user-secrets set "Jwt:Audience" "AiEnterprise"
    Pop-Location
}

# Gateway-specific: API key
Push-Location "src\AiEnterprise.Gateway"
dotnet user-secrets set "Gateway:ApiKey" $GATEWAY_KEY
Pop-Location

# DocumentIntelligence-specific: NVIDIA key
Push-Location "src\AiEnterprise.DocumentIntelligence"
dotnet user-secrets set "Nvidia:ApiKey" $NVIDIA_KEY
Pop-Location

Write-Host ""
Write-Host "Done. Restart all microservices to pick up the new secrets." -ForegroundColor Green
Write-Host "Then set VITE_GATEWAY_API_KEY=$GATEWAY_KEY in ecri-dashboard/.env.development.local" -ForegroundColor Yellow
