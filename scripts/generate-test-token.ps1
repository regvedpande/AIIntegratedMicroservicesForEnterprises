<#
.SYNOPSIS
    Generates a JWT token for testing the AiEnterprise ECRI API.

.PARAMETER JwtKey
    The JWT signing key (same as Jwt:Key in user-secrets).

.PARAMETER EnterpriseId
    Optional enterprise ID to embed in token. Defaults to a new random GUID.

.PARAMETER Role
    User role. One of: Admin, ComplianceOfficer, Analyst, Viewer. Defaults to Admin.

.EXAMPLE
    .\scripts\generate-test-token.ps1 -JwtKey "your-secret-key"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$JwtKey,

    [string]$EnterpriseId = [System.Guid]::NewGuid().ToString(),
    [string]$UserId = [System.Guid]::NewGuid().ToString(),
    [string]$Email = "admin@test.com",
    [string]$Role = "Admin",
    [int]$ExpiryHours = 8
)

# Create a temp C# script to generate the token
$script = @"
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

var key = "$JwtKey";
var creds = new SigningCredentials(
    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
    SecurityAlgorithms.HmacSha256);

var token = new JwtSecurityToken(
    issuer: "AiEnterpriseGateway",
    audience: "AiEnterprise",
    claims: new[]
    {
        new Claim("user_id", "$UserId"),
        new Claim("enterprise_id", "$EnterpriseId"),
        new Claim(ClaimTypes.Role, "$Role"),
        new Claim(ClaimTypes.Email, "$Email"),
        new Claim(ClaimTypes.NameIdentifier, "$UserId")
    },
    expires: DateTime.UtcNow.AddHours($ExpiryHours),
    signingCredentials: creds
);

Console.WriteLine(new JwtSecurityTokenHandler().WriteToken(token));
"@

$tempDir = Join-Path $env:TEMP "aiep-token-gen"
New-Item -ItemType Directory -Force -Path $tempDir | Out-Null

$scriptFile = Join-Path $tempDir "Program.cs"
$script | Out-File -FilePath $scriptFile -Encoding UTF8

$projContent = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="7.0.0" />
    <PackageReference Include="Microsoft.IdentityModel.Tokens" Version="7.0.0" />
  </ItemGroup>
</Project>
"@

$projFile = Join-Path $tempDir "TokenGen.csproj"
$projContent | Out-File -FilePath $projFile -Encoding UTF8

Push-Location $tempDir
$token = dotnet run --project $projFile 2>&1 | Select-Object -Last 1
Pop-Location

Write-Host ""
Write-Host "JWT Token (expires in $ExpiryHours hours):" -ForegroundColor Cyan
Write-Host $token -ForegroundColor Green
Write-Host ""
Write-Host "Usage in curl:" -ForegroundColor Yellow
Write-Host "  curl -H 'Authorization: Bearer $token' -H 'X-Api-Key: <your-gateway-api-key>' http://localhost:5000/api/gateway/health"
Write-Host ""
Write-Host "Enterprise ID embedded: $EnterpriseId" -ForegroundColor Gray
