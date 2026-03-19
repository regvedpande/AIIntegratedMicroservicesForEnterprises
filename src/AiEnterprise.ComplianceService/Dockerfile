FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5001

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/AiEnterprise.ComplianceService/AiEnterprise.ComplianceService.csproj", "src/AiEnterprise.ComplianceService/"]
COPY ["src/AiEnterprise.Core/AiEnterprise.Core.csproj", "src/AiEnterprise.Core/"]
COPY ["src/AiEnterprise.Infrastructure/AiEnterprise.Infrastructure.csproj", "src/AiEnterprise.Infrastructure/"]
COPY ["src/AiEnterprise.Shared/AiEnterprise.Shared.csproj", "src/AiEnterprise.Shared/"]

RUN dotnet restore "src/AiEnterprise.ComplianceService/AiEnterprise.ComplianceService.csproj"
COPY . .
RUN dotnet publish "src/AiEnterprise.ComplianceService/AiEnterprise.ComplianceService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AiEnterprise.ComplianceService.dll"]
