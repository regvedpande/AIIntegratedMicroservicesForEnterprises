# AiEnterprise ECRI
### Enterprise Compliance & Risk Intelligence Platform

> **Most enterprises discover compliance violations during audits — after the fines are issued.**
> ECRI closes that gap: continuous monitoring, AI-powered document analysis, real-time vendor risk scoring, and a cryptographically tamper-proof audit trail, all behind a single hardened API gateway.

---

## Why This Exists

| Enterprise Pain Point | Industry Impact | What ECRI Does |
|---|---|---|
| Manual compliance monitoring | 73 % of enterprises fail audits | Continuous rule evaluation — GDPR, SOX, HIPAA, PCI-DSS |
| Contract review takes 3–4 weeks | Hidden risks cost millions | Claude AI analysis in seconds |
| Vendor risk assessed once per year | 60 % of breaches are third-party | Continuous 7-dimension vendor risk scoring |
| Audit logs are tampered with | Regulatory penalties, no evidence | SHA-256 cryptographic integrity per entry |
| Thousands of noisy alerts | 70 % of alerts go unread | Intelligent deduplication and priority routing |

---

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│               API Gateway  :5000                         │
│   JWT Bearer · X-Api-Key · Rate Limiting (100 req/min)  │
│   Security Headers · CORS · Structured Routing           │
└────────────────────┬─────────────────────────────────────┘
                     │
       ┌─────────────┼──────────────────────┐
       ▼             ▼            ▼          ▼
┌──────────┐  ┌──────────┐  ┌────────┐  ┌────────┐
│Compliance│  │Document  │  │  Risk  │  │ Audit  │
│ Service  │  │Intellig. │  │Scoring │  │Service │
│  :5001   │  │  :5002   │  │ :5003  │  │ :5004  │
└──────────┘  └──────────┘  └────────┘  └────────┘
                                              │
                                       ┌──────┴──────┐
                                       │Notification │
                                       │    Hub      │
                                       │   :5005     │
                                       └─────────────┘
                          │                    │
                 ┌────────▼────────────────────┘
                 │   SQL Server  ·  Redis Cache  │
                 └───────────────────────────────┘
```

Each service is an independent ASP.NET Core 8 process.
All inter-service communication is HTTP with JWT forwarding from the gateway.
The gateway is the **only** externally exposed entry point.

---

## Service Reference

### 1 — API Gateway `localhost:5000`

The single entry point for every client request.

- Dual authentication: JWT Bearer (user identity) + `X-Api-Key` header (gateway access control)
- Fixed-window rate limiting: 100 req/min globally, 10 req/min for AI document analysis
- Security headers on every response: `Content-Security-Policy`, `Strict-Transport-Security`, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy`
- Routes requests to downstream services by path prefix, forwards the `Authorization` header
- `/api/gateway/health` aggregates health from all five downstream services in parallel

### 2 — Compliance Service `localhost:5001`

Real-time compliance rule evaluation against regulatory frameworks.

**Supported frameworks:** GDPR · SOX · HIPAA · PCI-DSS

**Built-in rules (10 total):**

| Rule Code | Regulation | Severity |
|---|---|---|
| GDPR-ART5-1F | Data Minimisation | High |
| GDPR-ART17 | Right to Erasure | Critical |
| GDPR-ART32 | Security of Processing | Critical |
| GDPR-ART33 | Breach Notification (72 h) | Critical |
| SOX-302 | CEO/CFO Financial Certification | Critical |
| SOX-404 | Internal Controls Assessment | High |
| HIPAA-164.312A | PHI Access Controls | Critical |
| HIPAA-164.312E | PHI Transmission Encryption | Critical |
| PCI-REQ3 | Cardholder Data Encryption | Critical |
| PCI-REQ8 | Multi-Factor Authentication | High |

Violations move through a lifecycle: **Detected → In Remediation → Resolved**.
Each violation stores the rule code, affected resource, evidence JSON, severity, and timestamps.
A per-framework compliance score (0–100 %) is calculated from the ratio of passing rules.

### 3 — Document Intelligence `localhost:5002`

AI-powered document risk analysis using **Claude claude-sonnet-4-6** (Anthropic).

**Supported document types:** Contracts · Data Processing Agreements · NDAs · Policies · SLAs · Invoices

Each analysis returns:

| Field | Description |
|---|---|
| `overallRiskLevel` | Low / Medium / High / Critical |
| `riskScore` | Numeric score 0–100 |
| `executiveSummary` | 2–3 sentence summary for leadership |
| `findings[]` | Specific risk findings with clause references and remediation |
| `complianceConcerns[]` | Regulatory references (GDPR Art. 28, HIPAA 164.312, etc.) |
| `recommendations[]` | Prioritised remediation actions |

Security controls: SHA-256 content integrity hash on every uploaded document, path traversal prevention via `SanitizeFileName()`, base64 content validation, 10 MB size cap.

### 4 — Risk Scoring `localhost:5003`

Multi-dimensional vendor risk assessment and behavioural anomaly detection.

**Vendor Risk — 7 dimensions:**

| Dimension | Weight | What It Measures |
|---|---|---|
| Data Security Practices | 25 % | DPA signed, encryption in place |
| Compliance Certifications | 20 % | SOC 2, ISO 27001, DPA |
| Incident History | 15 % | Past breach count and severity |
| Contractual Protections | 15 % | Audit rights, liability caps, breach notification SLAs |
| Geographic Risk | 10 % | Jurisdiction adequacy (GDPR Chapter V) |
| Financial Stability | 5 % | Vendor financial health signals |
| Access Privilege Level | 10 % | Depth of access granted to enterprise systems |

**Behavioural Anomaly Detection:** mass data downloads · off-hours admin access · privilege escalation · repeated authentication failures.

### 5 — Audit Service `localhost:5004`

Tamper-proof audit trail with cryptographic integrity verification.

- Every log entry is hashed with SHA-256 over `(entryId + enterpriseId + action + resourceId + timestamp)`
- `GET /api/audit/verify/{id}` re-computes the hash and compares — any tampering is detected immediately
- Flexible query API: filter by action, resource type, user ID, and date range with pagination
- Compliance report generation: one-click reports per framework and period, stored and retrievable

### 6 — Notification Hub `localhost:5005`

Intelligent alert routing that fights alert fatigue.

- **Deduplication:** per-alert-type cooldown windows in Redis (15 min for Critical, 30 min for High, 60 min for others) prevent alert storms
- **Priority routing:** Critical/High → webhook + email; Medium/Low → in-app queue only
- **Email delivery:** real SMTP integration (works with SendGrid, Mailgun, AWS SES, Office 365)
- **Webhook:** Slack and Microsoft Teams compatible JSON payloads
- **Acknowledgment workflow:** records who acknowledged which alert and when

---

## Technology Stack

| Layer | Technology | Version |
|---|---|---|
| Runtime | .NET / ASP.NET Core | 8.0 |
| AI Model | Claude (Anthropic SDK) | claude-sonnet-4-6 |
| ORM | Dapper | 2.1 |
| Database | SQL Server | 2022 |
| Cache | Redis | 7 |
| Authentication | JWT Bearer + API Key | — |
| Rate Limiting | ASP.NET Core RateLimiting | built-in |
| Containerisation | Docker + Docker Compose | 3.9 |
| API Docs | Swagger / OpenAPI | — |
| Frontend | React 18 + TypeScript + Material-UI | Vite |

---

## Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Anthropic API Key](https://console.anthropic.com) — needed by the Document Intelligence service

### Quick Start with Docker (recommended)

```bash
# 1. Clone
git clone https://github.com/your-org/AIIntegratedMicroservicesForEnterprises
cd AIIntegratedMicroservicesForEnterprises

# 2. Configure secrets — fill in every <PLACEHOLDER> value
cp .env.template .env
$EDITOR .env

# 3. Start everything (SQL Server, Redis, all 6 services)
docker compose up -d

# 4. Verify
curl http://localhost:5000/api/gateway/health
```

Expected health response:
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

### Local Development Setup

```bash
# ── Gateway ──────────────────────────────────────────────────
cd src/AiEnterprise.Gateway
dotnet user-secrets set "Jwt:Key" "your-dev-secret-key-at-least-32-characters"
dotnet user-secrets set "Gateway:ApiKey" "your-dev-api-key"

# ── Document Intelligence ─────────────────────────────────────
cd ../AiEnterprise.DocumentIntelligence
dotnet user-secrets set "Jwt:Key" "your-dev-secret-key-at-least-32-characters"
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."

# Run individual services
dotnet run --project src/AiEnterprise.Gateway
dotnet run --project src/AiEnterprise.DocumentIntelligence
# ... or use the Makefile:
make dev
```

### Frontend

```bash
cd ecri-dashboard
npm install
npm run dev        # http://localhost:5173
```

---

## Environment Variables

All configuration is environment-variable driven.
**Never store secrets in `appsettings.json`** — that file is gitignored; only `appsettings.template.json` is committed.

| Variable | Service | Required | Description |
|---|---|---|---|
| `Jwt__Key` | All | Yes | JWT signing key — minimum 32 characters |
| `Gateway__ApiKey` | Gateway | Yes | Value expected in `X-Api-Key` header |
| `Anthropic__ApiKey` | DocumentIntelligence | Yes | Anthropic API key (`sk-ant-...`) |
| `ConnectionStrings__DefaultConnection` | All | Yes | SQL Server connection string |
| `ConnectionStrings__Redis` | All | Yes | Redis connection string |
| `Notifications__WebhookUrl` | NotificationHub | No | Slack / Teams incoming webhook URL |
| `Notifications__AlertEmailRecipients` | NotificationHub | No | Comma-separated email list |
| `Notifications__Smtp__Host` | NotificationHub | No | SMTP host (e.g. `smtp.sendgrid.net`) |
| `Notifications__Smtp__Port` | NotificationHub | No | SMTP port — default `587` |
| `Notifications__Smtp__Username` | NotificationHub | No | SMTP auth username |
| `Notifications__Smtp__Password` | NotificationHub | No | SMTP auth password |

---

## API Examples

### Authenticate (get a JWT)

```http
POST /api/auth/login
Content-Type: application/json

{ "email": "admin@acme.com", "password": "..." }
```

### Run a Compliance Check

```http
POST /api/gateway/compliance/check
Authorization: Bearer {jwt}
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

### Analyse a Document with Claude AI

```http
POST /api/gateway/documents/analyze
Authorization: Bearer {jwt}
X-Api-Key: {gateway_api_key}
Content-Type: application/json

{
  "enterpriseId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "uploadedByUserId": "user-uuid-here",
  "documentType": 1,
  "fileName": "vendor-contract.pdf",
  "base64Content": "<base64-encoded-document-content>"
}
```

Response:
```json
{
  "documentId": "uuid",
  "overallRiskLevel": "High",
  "riskScore": 72.5,
  "executiveSummary": "This vendor contract contains several high-risk clauses including an uncapped liability waiver and a data retention clause that conflicts with GDPR Art. 17.",
  "findingsCount": 5,
  "complianceConcernsCount": 3
}
```

### Assess Vendor Risk

```http
POST /api/gateway/risk/vendors/assess
Authorization: Bearer {jwt}
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

### Query the Tamper-Proof Audit Log

```http
GET /api/gateway/audit/{enterpriseId}/query?from=2025-01-01T00:00:00Z&to=2025-12-31T23:59:59Z
Authorization: Bearer {jwt}
X-Api-Key: {gateway_api_key}
```

### Verify Audit Entry Integrity

```http
GET /api/gateway/audit/verify/{auditEntryId}
Authorization: Bearer {jwt}
X-Api-Key: {gateway_api_key}
```

---

## Security Design

### Authentication & Authorisation

- **Dual-layer auth:** JWT Bearer (user identity, 60-min expiry) + `X-Api-Key` header (gateway access)
- **Role-based access control:** `Admin` · `ComplianceOfficer` · `Analyst` · `Viewer`
- Sensitive endpoints (resolve violations, generate reports, verify audit integrity) are restricted to `Admin` and `ComplianceOfficer` roles
- Short-lived JWTs with refresh token support

### Defence in Depth

| Control | Implementation |
|---|---|
| Transport security | HTTPS enforced, `Strict-Transport-Security` header |
| Input validation | Null checks, size limits, MIME type validation on document uploads |
| SQL injection prevention | Parameterised queries via Dapper throughout — no string concatenation |
| Path traversal prevention | `SecurityUtility.SanitizeFileName()` on all file names before storage |
| Audit tampering detection | SHA-256 hash per entry; re-verified on demand |
| Rate limiting | Fixed-window per-IP; tighter limit on AI (expensive) endpoints |
| CORS | Origin whitelist, configurable per environment |
| Security headers | `Content-Security-Policy`, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy` on every response |
| Container hardening | Non-root user in every Dockerfile |
| Secrets management | Never in source code; `dotnet user-secrets` for dev, env vars / Azure Key Vault for production |

---

## Project Structure

```
src/
├── AiEnterprise.Gateway/                  # :5000 — API Gateway
│   ├── Controllers/GatewayController.cs  # Route forwarding to microservices
│   ├── Middleware/AuthMiddleware.cs       # X-Api-Key validation
│   └── Program.cs                        # Rate limiting, JWT, CORS
│
├── AiEnterprise.Core/                     # Shared domain — no external dependencies
│   ├── Models/                           # Domain entities
│   ├── DTOs/                             # Request/response contracts
│   ├── Interfaces/                       # Service abstractions
│   └── Enums/                            # Domain enumerations
│
├── AiEnterprise.Infrastructure/           # Data access (SQL Server, Redis)
│   ├── Configuration/DapperContext.cs
│   ├── Caching/RedisCacheService.cs
│   ├── Database/DatabaseInitializer.cs   # Schema creation on startup
│   └── Extensions/InfrastructureServices.cs
│
├── AiEnterprise.Shared/                   # Cross-cutting utilities
│   ├── Constants/AppConstants.cs
│   ├── Middleware/SecurityHeadersMiddleware.cs
│   └── Utilities/SecurityUtility.cs      # SHA-256 hashing, password hashing
│
├── AiEnterprise.ComplianceService/        # :5001 — Compliance monitoring
│   ├── Services/ComplianceRuleEngine.cs  # GDPR / SOX / HIPAA / PCI-DSS rules
│   └── Controllers/ComplianceController.cs
│
├── AiEnterprise.DocumentIntelligence/    # :5002 — AI document analysis
│   ├── Services/ClaudeDocumentAnalyzer.cs  # Anthropic Claude API integration
│   └── Services/DocumentAnalysisService.cs
│
├── AiEnterprise.RiskScoring/             # :5003 — Vendor & behavioural risk
│   └── Services/VendorRiskScoringEngine.cs
│
├── AiEnterprise.AuditService/            # :5004 — Tamper-proof audit trail
│   └── Services/TamperProofAuditService.cs
│
└── AiEnterprise.NotificationHub/         # :5005 — Intelligent alert routing
    └── Services/AlertNotificationService.cs

ecri-dashboard/                           # React 18 + TypeScript frontend
```

---

## Compliance Frameworks

| Framework | Key Rules | Maximum Penalty |
|---|---|---|
| GDPR | Art.5 (minimisation), Art.17 (erasure), Art.32 (encryption), Art.33 (breach notification) | €20 M or 4 % global revenue |
| SOX | Section 302 (CEO/CFO certification), Section 404 (internal controls) | Criminal penalties, up to $5 M |
| HIPAA | §164.312(a) (PHI access controls), §164.312(e) (PHI transmission) | Up to $1.9 M per category per year |
| PCI-DSS | Req. 3 (cardholder data encryption), Req. 8 (MFA enforcement) | Card processing termination + $5K–$100K/month |

---

## Contributing

1. **Never commit secrets** — `appsettings.json` is in `.gitignore`; use `dotnet user-secrets` locally
2. **Parameterised queries only** — no string-concatenated SQL; use Dapper parameters
3. **Security headers** — every new service must register `SecurityHeadersMiddleware`
4. **New compliance rules** — add to `ComplianceRuleEngine.BuildBuiltInRules()` with a rule code, regulatory reference, and remediation guidance
5. **Test coverage** — unit tests for rule engine logic; integration tests for API endpoints with a real database

---

## License

MIT License — see [LICENSE](LICENSE) for details.

---

*Document Intelligence powered by [Claude claude-sonnet-4-6](https://www.anthropic.com) (Anthropic).*
