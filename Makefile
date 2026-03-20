# ============================================================
# AiEnterprise ECRI — Developer Makefile
# ============================================================
# Prerequisites: Docker, .NET 8 SDK, make
#
# Quick start:
#   cp .env.template .env   # fill in your secrets
#   make up                 # build images + start everything
#   make health             # verify all services are healthy
# ============================================================

.PHONY: help up down restart logs health build clean \
        restore compile publish dev secrets

# ── Default target ────────────────────────────────────────────
help:
	@echo ""
	@echo "AiEnterprise ECRI — available targets:"
	@echo ""
	@echo "  Docker:"
	@echo "    make up          Build images and start all services"
	@echo "    make down        Stop and remove containers"
	@echo "    make restart     down + up"
	@echo "    make logs        Tail all container logs"
	@echo "    make health      Check gateway health endpoint"
	@echo ""
	@echo "  .NET:"
	@echo "    make restore     dotnet restore (all projects)"
	@echo "    make compile     dotnet build Release (zero warnings)"
	@echo "    make publish     dotnet publish all services to ./publish/"
	@echo "    make dev         Run all 6 services locally via dotnet run"
	@echo ""
	@echo "  Setup:"
	@echo "    make secrets     Configure dotnet user-secrets (Linux/Mac)"
	@echo "    make clean       Remove build artifacts and Docker volumes"
	@echo ""

# ── Docker targets ────────────────────────────────────────────
up:
	@test -f .env || (echo "ERROR: .env not found. Run: cp .env.template .env" && exit 1)
	docker compose up -d --build

down:
	docker compose down

restart: down up

logs:
	docker compose logs -f

health:
	@echo "Waiting for gateway..."
	@sleep 2
	curl -sf http://localhost:5000/api/gateway/health | python3 -m json.tool 2>/dev/null || \
	  curl -sf http://localhost:5000/api/gateway/health

# ── .NET targets ─────────────────────────────────────────────
restore:
	dotnet restore AiEnterpriseSolution.sln

compile: restore
	dotnet build AiEnterpriseSolution.sln --configuration Release --no-restore -warnaserror

publish: compile
	@mkdir -p publish
	@for svc in Gateway ComplianceService DocumentIntelligence RiskScoring AuditService NotificationHub; do \
	  echo "Publishing AiEnterprise.$$svc..."; \
	  dotnet publish src/AiEnterprise.$$svc/AiEnterprise.$$svc.csproj \
	    --configuration Release --no-build \
	    --output publish/$$svc -p:UseAppHost=false; \
	done

# Run all 6 services locally in parallel (requires dotnet user-secrets configured)
dev:
	@echo "Starting 6 services... (Ctrl+C to stop all)"
	@trap 'kill 0' INT; \
	  dotnet run --project src/AiEnterprise.ComplianceService    --no-launch-profile & \
	  dotnet run --project src/AiEnterprise.DocumentIntelligence --no-launch-profile & \
	  dotnet run --project src/AiEnterprise.RiskScoring          --no-launch-profile & \
	  dotnet run --project src/AiEnterprise.AuditService         --no-launch-profile & \
	  dotnet run --project src/AiEnterprise.NotificationHub      --no-launch-profile & \
	  dotnet run --project src/AiEnterprise.Gateway              --no-launch-profile & \
	  wait

# ── Setup targets ────────────────────────────────────────────
secrets:
	@chmod +x scripts/setup.sh
	./scripts/setup.sh

# ── Cleanup ───────────────────────────────────────────────────
clean:
	docker compose down -v --remove-orphans
	dotnet clean AiEnterpriseSolution.sln
	rm -rf publish/
