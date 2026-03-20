#!/usr/bin/env bash
# ============================================================
# AiEnterprise ECRI — Local development secrets setup
# Linux / macOS equivalent of setup-secrets.ps1
#
# Usage:
#   chmod +x scripts/setup.sh
#   ./scripts/setup.sh
#   ./scripts/setup.sh --anthropic-key sk-ant-...
# ============================================================
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# ── Argument parsing ─────────────────────────────────────────
JWT_KEY=""
SQL_CONN="Server=(localdb)\\mssqllocaldb;Database=AiEnterpriseDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
REDIS_CONN="localhost:6379"
GATEWAY_KEY=""
ANTHROPIC_KEY=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --jwt-key)        JWT_KEY="$2";        shift 2 ;;
    --sql-conn)       SQL_CONN="$2";       shift 2 ;;
    --redis-conn)     REDIS_CONN="$2";     shift 2 ;;
    --gateway-key)    GATEWAY_KEY="$2";    shift 2 ;;
    --anthropic-key)  ANTHROPIC_KEY="$2";  shift 2 ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

# ── Auto-generate secrets if not supplied ────────────────────
if [[ -z "$JWT_KEY" ]]; then
  JWT_KEY=$(openssl rand -base64 48)
  echo -e "\033[33m[generated] JWT Key: $JWT_KEY\033[0m"
  echo -e "\033[31mSAVE THIS KEY — all services must share the same value!\033[0m"
fi

if [[ -z "$GATEWAY_KEY" ]]; then
  GATEWAY_KEY=$(openssl rand -hex 32)
  echo -e "\033[33m[generated] Gateway API Key: $GATEWAY_KEY\033[0m"
fi

if [[ -z "$ANTHROPIC_KEY" ]]; then
  echo -e "\033[33mWARNING: --anthropic-key not supplied. Document Intelligence will not function.\033[0m"
  echo -e "\033[33mGet your key at https://console.anthropic.com\033[0m"
  ANTHROPIC_KEY="REPLACE_WITH_YOUR_ANTHROPIC_API_KEY"
fi

# ── Helper ────────────────────────────────────────────────────
set_secret() {
  local svc_path="$1"
  local key="$2"
  local value="$3"
  if dotnet user-secrets set "$key" "$value" --project "$svc_path" > /dev/null 2>&1; then
    echo -e "    \033[32m[ok]\033[0m  $key"
  else
    echo -e "    \033[31m[fail]\033[0m $key" >&2
  fi
}

echo ""
echo -e "\033[36m=== AiEnterprise ECRI — configuring user-secrets ===\033[0m"

# ── Gateway ──────────────────────────────────────────────────
echo -e "\n\033[36m  AiEnterprise.Gateway\033[0m"
GW="$REPO_ROOT/src/AiEnterprise.Gateway"
set_secret "$GW" "Jwt:Key"               "$JWT_KEY"
set_secret "$GW" "Gateway:ApiKey"        "$GATEWAY_KEY"
set_secret "$GW" "ConnectionStrings:Redis" "$REDIS_CONN"

# ── ComplianceService ─────────────────────────────────────────
echo -e "\n\033[36m  AiEnterprise.ComplianceService\033[0m"
CS="$REPO_ROOT/src/AiEnterprise.ComplianceService"
set_secret "$CS" "Jwt:Key"                              "$JWT_KEY"
set_secret "$CS" "ConnectionStrings:DefaultConnection"  "$SQL_CONN"
set_secret "$CS" "ConnectionStrings:Redis"              "$REDIS_CONN"

# ── DocumentIntelligence ─────────────────────────────────────
echo -e "\n\033[36m  AiEnterprise.DocumentIntelligence\033[0m"
DI="$REPO_ROOT/src/AiEnterprise.DocumentIntelligence"
set_secret "$DI" "Jwt:Key"                              "$JWT_KEY"
set_secret "$DI" "Anthropic:ApiKey"                     "$ANTHROPIC_KEY"
set_secret "$DI" "ConnectionStrings:DefaultConnection"  "$SQL_CONN"
set_secret "$DI" "ConnectionStrings:Redis"              "$REDIS_CONN"

# ── RiskScoring ───────────────────────────────────────────────
echo -e "\n\033[36m  AiEnterprise.RiskScoring\033[0m"
RS="$REPO_ROOT/src/AiEnterprise.RiskScoring"
set_secret "$RS" "Jwt:Key"                              "$JWT_KEY"
set_secret "$RS" "ConnectionStrings:DefaultConnection"  "$SQL_CONN"
set_secret "$RS" "ConnectionStrings:Redis"              "$REDIS_CONN"

# ── AuditService ─────────────────────────────────────────────
echo -e "\n\033[36m  AiEnterprise.AuditService\033[0m"
AS="$REPO_ROOT/src/AiEnterprise.AuditService"
set_secret "$AS" "Jwt:Key"                              "$JWT_KEY"
set_secret "$AS" "ConnectionStrings:DefaultConnection"  "$SQL_CONN"
set_secret "$AS" "ConnectionStrings:Redis"              "$REDIS_CONN"

# ── NotificationHub ───────────────────────────────────────────
echo -e "\n\033[36m  AiEnterprise.NotificationHub\033[0m"
NH="$REPO_ROOT/src/AiEnterprise.NotificationHub"
set_secret "$NH" "Jwt:Key"                              "$JWT_KEY"
set_secret "$NH" "ConnectionStrings:DefaultConnection"  "$SQL_CONN"
set_secret "$NH" "ConnectionStrings:Redis"              "$REDIS_CONN"

echo ""
echo -e "\033[32m=== Setup complete! ===\033[0m"
echo ""
echo "Next steps:"
echo "  1. Start Redis:    docker run -d -p 6379:6379 redis:7-alpine"
echo "  2. Create DB:      sqlcmd -S '(localdb)\\mssqllocaldb' -Q 'CREATE DATABASE AiEnterpriseDb'"
echo "  3. Start services: make dev   (or run each with 'dotnet run')"
echo ""
echo -e "\033[33mGateway API Key: $GATEWAY_KEY\033[0m"
echo "(Pass this as X-Api-Key header in all API requests)"
echo ""
