# Developer Setup Guide

This guide walks you through getting the ECRI platform running from a fresh clone.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (LocalDB, Express, or full) — or Docker Desktop
- Redis — or Docker Desktop
- [Anthropic API Key](https://console.anthropic.com) (for Document Intelligence service)

---

## Option A: Docker (Recommended — Zero Config)

```bash
# 1. Copy and fill in secrets
cp .env.template .env
# Edit .env with your values (see comments inside)

# 2. Start everything
docker compose up -d

# 3. Verify all services are healthy
curl http://localhost:5000/api/gateway/health
```

The first startup automatically creates all database tables via `DatabaseInitializer`.

---

## Option B: Local Development (dotnet run)

### Step 1: Configure secrets via dotnet user-secrets

Each service uses .NET User Secrets to keep secrets out of source control.
Run these commands from the **repository root**:

```powershell
# Run the setup script (Windows PowerShell)
.\scripts\setup-secrets.ps1
```

Or manually set secrets per service:

```bash
# Gateway
cd src/AiEnterprise.Gateway
dotnet user-secrets set "Jwt:Key"       "<your-secret-min-32-chars>"
dotnet user-secrets set "Gateway:ApiKey" "<your-api-key>"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"

# ComplianceService
cd ../AiEnterprise.ComplianceService
dotnet user-secrets set "Jwt:Key" "<SAME-key-as-gateway>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=AiEnterpriseDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"

# DocumentIntelligence
cd ../AiEnterprise.DocumentIntelligence
dotnet user-secrets set "Jwt:Key" "<SAME-key-as-gateway>"
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=AiEnterpriseDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"

# RiskScoring
cd ../AiEnterprise.RiskScoring
dotnet user-secrets set "Jwt:Key" "<SAME-key-as-gateway>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=AiEnterpriseDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"

# AuditService
cd ../AiEnterprise.AuditService
dotnet user-secrets set "Jwt:Key" "<SAME-key-as-gateway>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=AiEnterpriseDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"

# NotificationHub
cd ../AiEnterprise.NotificationHub
dotnet user-secrets set "Jwt:Key" "<SAME-key-as-gateway>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.;Database=AiEnterpriseDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379"
```

### Step 2: Database Setup

The database schema is **automatically created on first startup** via `DatabaseInitializer`.
You only need an empty database (or let SQL Server create it):

```sql
-- Run this once on your SQL Server instance
CREATE DATABASE AiEnterpriseDb;
```

If using SQL Server LocalDB (the default for VS):
```
Server=(localdb)\mssqllocaldb;Database=AiEnterpriseDb;Integrated Security=True;TrustServerCertificate=True;
```

### Step 3: Start Redis

```bash
# Using Docker (easiest)
docker run -d -p 6379:6379 redis:7-alpine

# Or install Redis locally and start it
redis-server
```

### Step 4: Run Services

Open 6 terminal windows (or use Visual Studio multiple startup):

```bash
# Terminal 1
cd src/AiEnterprise.ComplianceService && dotnet run

# Terminal 2
cd src/AiEnterprise.DocumentIntelligence && dotnet run

# Terminal 3
cd src/AiEnterprise.RiskScoring && dotnet run

# Terminal 4
cd src/AiEnterprise.AuditService && dotnet run

# Terminal 5
cd src/AiEnterprise.NotificationHub && dotnet run

# Terminal 6 - Start gateway last
cd src/AiEnterprise.Gateway && dotnet run
```

### Step 5: Verify

```bash
curl http://localhost:5000/api/gateway/health
```

Expected response:
```json
{
  "gateway": "Healthy",
  "overallStatus": "Healthy",
  "services": {
    "ComplianceService": "Healthy",
    "DocumentIntelligence": "Healthy",
    "RiskScoring": "Healthy",
    "AuditService": "Healthy",
    "NotificationHub": "Healthy"
  }
}
```

---

## Swagger UI (API Documentation)

Each service has its own Swagger UI in Development mode:

| Service | URL |
|---|---|
| Gateway | http://localhost:5000/swagger |
| Compliance | http://localhost:5001/swagger |
| Document Intelligence | http://localhost:5002/swagger |
| Risk Scoring | http://localhost:5003/swagger |
| Audit | http://localhost:5004/swagger |
| Notifications | http://localhost:5005/swagger |

---

## Generating a JWT Token for Testing

Since there's no IdentityService yet, generate a test JWT:

```csharp
// Use this C# snippet to generate a test token
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var key = "<YOUR_JWT_KEY>"; // same as Jwt:Key
var creds = new SigningCredentials(
    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
    SecurityAlgorithms.HmacSha256);

var token = new JwtSecurityToken(
    issuer: "AiEnterpriseGateway",
    audience: "AiEnterprise",
    claims: new[]
    {
        new Claim("user_id", Guid.NewGuid().ToString()),
        new Claim("enterprise_id", Guid.NewGuid().ToString()),
        new Claim(ClaimTypes.Role, "Admin"),
        new Claim(ClaimTypes.Email, "admin@test.com")
    },
    expires: DateTime.UtcNow.AddHours(8),
    signingCredentials: creds
);

Console.WriteLine(new JwtSecurityTokenHandler().WriteToken(token));
```

Or use the PowerShell script: `.\scripts\generate-test-token.ps1 -JwtKey "<your-key>"`

---

## Common Issues

| Error | Cause | Fix |
|---|---|---|
| `Jwt:Key is not configured` | Secrets not set | Run `setup-secrets.ps1` or set user-secrets manually |
| `Connection refused` on port 6379 | Redis not running | `docker run -d -p 6379:6379 redis:7-alpine` |
| SQL Server login failed | Wrong connection string | Check `DefaultConnection` in user-secrets |
| `ComplianceService: Unreachable` | Service not started | Start all 5 services before the gateway |
| 401 Unauthorized | Missing X-Api-Key header | Add `X-Api-Key: <your-gateway-api-key>` header |
