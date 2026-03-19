# AiEnterprise ECRI — Enterprise Compliance & Risk Intelligence Platform

> **Solving the enterprise compliance blind spot with AI.**
> Most enterprises only discover compliance violations during audits — by then, the fines have already been issued.
> ECRI monitors continuously, analyzes documents with AI, scores vendor risk in real-time, and maintains a tamper-proof audit trail.

---

## The Problem This Solves

| Enterprise Pain Point | Industry Impact | ECRI Solution |
|---|---|---|
| Manual compliance monitoring | 73% of enterprises fail audits | Continuous rule-based monitoring (GDPR, SOX, HIPAA, PCI-DSS) |
| Contract review takes 3-4 weeks | Hidden risks cost millions | AI document analysis in seconds using Claude |
| Vendor risk assessed once/year | 60% of breaches are third-party | Continuous 7-dimension vendor risk scoring |
| Audit logs get tampered with | Regulatory penalties, no evidence | SHA-256 tamper-proof audit trail |
| Alert fatigue from noisy tools | 70% of alerts are ignored | Intelligent deduplication & priority routing |

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│            API Gateway  :5000                       │
│   JWT Auth + API Key + Rate Limiting                │
│   Security Headers + CORS + Request Routing         │
└───────────┬─────────────────────────────────────────┘
            │
    ┌───────┼───────────────────────────┐
    ▼       ▼           ▼              ▼
┌───────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐
│Comp.  │ │Document  │ │  Risk    │ │  Audit   │
│Service│ │Intelli.  │ │ Scoring  │ │ Service  │
│:5001  │ │  :5002   │ │  :5003   │ │  :5004   │
└───────┘ └──────────┘ └──────────┘ └──────────┘
                                          │
                                    ┌─────┴──────┐
                                    │Notification│
                                    │   Hub      │
                                    │   :5005    │
                                    └────────────┘
                    │                     │
              ┌─────▼──────────────┐      │
              │   SQL Server       │◄─────┘
              │   Redis Cache      │
              └────────────────────┘
```

---

## Services

### 1. API Gateway (Port 5000)
The single entry point for all enterprise clients.
- **Dual authentication**: JWT Bearer + X-Api-Key header
- **Rate limiting**: 100 req/min globally, 10 req/min for AI operations
- **Security headers**: CSP, HSTS, X-Frame-Options, Referrer-Policy
- **Route forwarding**: Proxies to all downstream services
- **Health aggregation**: Single health endpoint for all services

### 2. Compliance Service (Port 5001)
Real-time compliance monitoring against multiple frameworks.
- **Frameworks**: GDPR, SOX, HIPAA, PCI-DSS (ISO 27001, CCPA, NIST planned)
- **Built-in rules**: 10 critical rules across 4 frameworks (extensible)
- **Key rules**: GDPR Art.17 (Right to Erasure), GDPR Art.32 (Encryption), SOX 302/404, HIPAA PHI Access, PCI-DSS MFA
- **Violation lifecycle**: Detect → Assign → Remediate → Resolve
- **Compliance scoring**: Per-framework compliance score (0-100%)

### 3. Document Intelligence (Port 5002)
AI-powered document risk analysis using **Claude claude-sonnet-4-6**.
- **Document types**: Contracts, DPAs, NDAs, Policies, SLAs, Invoices
- **Analysis includes**:
  - Overall risk level (Negligible/Low/Medium/High/Critical)
  - Risk score (0-100)
  - Specific risk findings with clause references
  - Compliance concerns (GDPR, SOX, HIPAA references)
  - Actionable remediation recommendations
- **Security**: SHA-256 content integrity hashing, path traversal prevention

### 4. Risk Scoring (Port 5003)
Multi-dimensional vendor risk assessment and behavioral anomaly detection.

**Vendor Risk (7 dimensions)**:
| Dimension | Weight | What it measures |
|---|---|---|
| Data Security | 25% | DPA, encryption, security practices |
| Compliance Certifications | 20% | SOC 2, ISO 27001, DPA |
| Incident History | 15% | Past breach history |
| Contractual Protection | 15% | Contractual safeguards |
| Geographic Risk | 10% | Jurisdiction/country risk |
| Financial Stability | 5% | Financial health |
| Access Privilege | 10% | Level of access granted |

**Behavioral Anomalies**: Mass data downloads, off-hours admin access, privilege escalation, brute-force auth failures.

### 5. Audit Service (Port 5004)
Tamper-proof audit trail with cryptographic integrity verification.
- **SHA-256 integrity hash** per entry (entry ID + enterprise + action + resource + timestamp)
- **Tamper detection**: `GET /api/audit/verify/{id}` re-computes and compares hash
- **Compliance reports**: Auto-generate reports per framework per period
- **Flexible query**: Filter by action, resource type, user, date range

### 6. Notification Hub (Port 5005)
Intelligent alert routing with deduplication and priority scoring.
- **Deduplication**: Cooldown windows per alert type to prevent storm alerts
- **Priority routing**: Critical/High → webhook + email; Low/Medium → in-app only
- **Acknowledgment workflow**: Track who acknowledged what and when
- **Multi-channel**: Webhook, email, in-app (Slack/Teams webhook compatible)

---

## Technology Stack

| Layer | Technology |
|---|---|
| Framework | .NET 8.0 (ASP.NET Core) |
| AI Model | Claude claude-sonnet-4-6 (Anthropic SDK) |
| Database | SQL Server (Dapper ORM) |
| Cache | Redis (StackExchange.Redis) |
| Auth | JWT Bearer + API Key |
| Rate Limiting | ASP.NET Core RateLimiting |
| Containerization | Docker + Docker Compose |
| API Docs | Swagger/OpenAPI |

---

## Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Anthropic API Key](https://console.anthropic.com)
- SQL Server or Docker (included in compose)

### Quick Start with Docker

```bash
# 1. Clone the repository
git clone https://github.com/your-org/AIIntegratedMicroservicesForEnterprises
cd AIIntegratedMicroservicesForEnterprises

# 2. Configure secrets (NEVER commit this file)
cp .env.template .env
nano .env  # Fill in all placeholder values

# 3. Start all services
docker compose up -d

# 4. Check health
curl http://localhost:5000/api/gateway/health
```

### Development Setup

```bash
# Configure per-service user secrets (development only)
cd src/AiEnterprise.Gateway
dotnet user-secrets set "Jwt:Key" "your-dev-secret-key-at-least-32-characters"
dotnet user-secrets set "Gateway:ApiKey" "your-dev-api-key"

cd ../AiEnterprise.DocumentIntelligence
dotnet user-secrets set "Jwt:Key" "your-dev-secret-key-at-least-32-characters"
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."

# Run a specific service
dotnet run --project src/AiEnterprise.Gateway
```

### Environment Variables

All configuration is environment-variable driven. Never put secrets in `appsettings.json`.

| Variable | Service | Description |
|---|---|---|
| `Jwt__Key` | All | JWT signing key (min 32 chars) |
| `Gateway__ApiKey` | Gateway | API key for X-Api-Key header |
| `Anthropic__ApiKey` | DocumentIntelligence | Anthropic API key |
| `ConnectionStrings__DefaultConnection` | All | SQL Server connection string |
| `ConnectionStrings__Redis` | All | Redis connection string |
| `Notifications__WebhookUrl` | NotificationHub | Slack/Teams webhook URL |

---

## API Usage Examples

### Run a Compliance Check
```http
POST /api/gateway/compliance/check
Authorization: Bearer {jwt_token}
X-Api-Key: {gateway_api_key}
Content-Type: application/json

{
  "enterpriseId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "framework": 1,
  "resourceType": "CustomerDatabase",
  "resourceId": "prod-customer-db",
  "resourceData": "{\"encryptionEnabled\":false,\"supportsDataDeletion\":true}"
}
```

Response:
```json
{
  "isCompliant": false,
  "violationsFound": 1,
  "complianceScore": 75.0,
  "violations": [{
    "ruleCode": "GDPR-ART32",
    "title": "GDPR Insufficient Security Measures",
    "severity": 4,
    "affectedResource": "prod-customer-db"
  }]
}
```

### Analyze a Document with AI
```http
POST /api/gateway/documents/analyze
Authorization: Bearer {jwt_token}
X-Api-Key: {gateway_api_key}
Content-Type: application/json

{
  "enterpriseId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "uploadedByUserId": "user-id-here",
  "documentType": 1,
  "fileName": "vendor-contract.pdf",
  "base64Content": "base64-encoded-document-content"
}
```

Response:
```json
{
  "documentId": "...",
  "overallRiskLevel": "High",
  "riskScore": 72.5,
  "executiveSummary": "This vendor contract contains several high-risk clauses...",
  "findingsCount": 5,
  "complianceConcernsCount": 3
}
```

### Assess Vendor Risk
```http
POST /api/gateway/risk/vendors/assess
Authorization: Bearer {jwt_token}
X-Api-Key: {gateway_api_key}
Content-Type: application/json

{
  "enterpriseId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "vendorName": "CloudStorage Corp",
  "vendorDomain": "cloudstorage.com",
  "serviceCategory": "Cloud Storage",
  "country": "United States",
  "dataTypesShared": ["PII", "Financial"],
  "hasSignedDPA": false,
  "hasSOC2": true,
  "hasISO27001": false
}
```

Response:
```json
{
  "riskLevel": "High",
  "compositeRiskScore": 65.3,
  "topRisks": [
    "No Data Processing Agreement signed",
    "No ISO 27001 certification",
    "Access to sensitive data: PII, Financial"
  ]
}
```

---

## Security Design

### Authentication & Authorization
- **Dual-layer auth**: JWT Bearer token (user identity) + X-Api-Key header (gateway access)
- **JWT validation**: Issuer, audience, lifetime, and signing key validated on every request
- **Role-based access**: Admin, ComplianceOfficer, Analyst, Viewer roles
- **Token security**: Short-lived JWTs (60 min) with refresh token support

### Secrets Management
- **Development**: `dotnet user-secrets` (never committed to git)
- **Production**: Environment variables or Azure Key Vault
- `appsettings.json` is in `.gitignore` — only `appsettings.template.json` is committed

### Defense in Depth
| Control | Implementation |
|---|---|
| Transport security | HTTPS enforced, HSTS header |
| Input validation | Null checks, size limits, MIME type validation |
| SQL injection | Parameterized queries via Dapper |
| Path traversal | `SanitizeFileName()` on all uploads |
| Audit tampering | SHA-256 integrity hash per entry |
| Rate limiting | Per-IP, per-endpoint limits |
| CORS | Origin whitelist, configurable per environment |
| Security headers | CSP, X-Frame-Options, X-Content-Type-Options |
| Container security | Non-root user in Docker containers |
| Secrets at rest | Never stored in source code |

---

## Project Structure

```
src/
├── AiEnterprise.Gateway/           # API Gateway - single entry point
│   ├── Controllers/GatewayController.cs   # Route forwarding to microservices
│   ├── Middleware/AuthMiddleware.cs        # API key validation
│   └── Program.cs                         # Rate limiting, JWT, CORS setup
│
├── AiEnterprise.Core/              # Shared domain models (no dependencies)
│   ├── Models/                    # Domain entities
│   ├── DTOs/                      # Request/response contracts
│   ├── Interfaces/                # Service contracts
│   ├── Enums/                     # Domain enumerations
│   └── Exceptions/                # Domain exceptions
│
├── AiEnterprise.Infrastructure/    # Data access (DB, Redis)
│   ├── Configuration/DapperContext.cs    # SQL Server connection
│   ├── Caching/RedisCacheService.cs      # Redis cache wrapper
│   ├── Database/DatabaseInitializer.cs   # Schema creation
│   └── Extensions/InfrastructureServices.cs
│
├── AiEnterprise.Shared/            # Cross-cutting utilities
│   ├── Constants/AppConstants.cs
│   ├── Middleware/SecurityHeadersMiddleware.cs
│   └── Utilities/SecurityUtility.cs      # SHA-256, password hashing
│
├── AiEnterprise.ComplianceService/ # :5001 - Compliance monitoring
│   ├── Services/ComplianceRuleEngine.cs  # GDPR/SOX/HIPAA/PCI-DSS rules
│   └── Controllers/ComplianceController.cs
│
├── AiEnterprise.DocumentIntelligence/ # :5002 - AI document analysis
│   ├── Services/ClaudeDocumentAnalyzer.cs  # Claude API integration
│   └── Services/DocumentAnalysisService.cs
│
├── AiEnterprise.RiskScoring/       # :5003 - Vendor & behavioral risk
│   └── Services/VendorRiskScoringEngine.cs
│
├── AiEnterprise.AuditService/      # :5004 - Tamper-proof audit trail
│   └── Services/TamperProofAuditService.cs
│
└── AiEnterprise.NotificationHub/   # :5005 - Intelligent alert routing
    └── Services/AlertNotificationService.cs
```

---

## Compliance Frameworks Supported

| Framework | Key Rules Implemented | Penalty Risk |
|---|---|---|
| GDPR | Art.5 (Data minimization), Art.17 (Right to erasure), Art.32 (Encryption), Art.33 (Breach notification) | Up to €20M or 4% global revenue |
| SOX | Section 302 (CEO/CFO certification), Section 404 (Internal controls) | Criminal penalties, up to $5M fine |
| HIPAA | 164.312(a) (PHI access controls), 164.312(e) (PHI transmission encryption) | Up to $1.9M per violation category per year |
| PCI-DSS | Req.3 (Cardholder data encryption), Req.8 (MFA enforcement) | Card processing termination, $5K-$100K/month |

---

## Contributing

1. Never commit secrets — `appsettings.json` is gitignored
2. Use `dotnet user-secrets` for local development
3. All new services must implement `SecurityHeadersMiddleware`
4. All database operations must use parameterized queries (Dapper)
5. New compliance rules should be added to `ComplianceRuleEngine.BuildBuiltInRules()`

---

## License

MIT License — see LICENSE file for details.

---

*Built with Claude claude-sonnet-4-6 (Anthropic) for AI document intelligence.*
