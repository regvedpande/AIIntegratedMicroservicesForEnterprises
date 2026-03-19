FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5003

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/AiEnterprise.RiskScoring/AiEnterprise.RiskScoring.csproj", "src/AiEnterprise.RiskScoring/"]
COPY ["src/AiEnterprise.Core/AiEnterprise.Core.csproj", "src/AiEnterprise.Core/"]
COPY ["src/AiEnterprise.Infrastructure/AiEnterprise.Infrastructure.csproj", "src/AiEnterprise.Infrastructure/"]
COPY ["src/AiEnterprise.Shared/AiEnterprise.Shared.csproj", "src/AiEnterprise.Shared/"]

RUN dotnet restore "src/AiEnterprise.RiskScoring/AiEnterprise.RiskScoring.csproj"
COPY . .
RUN dotnet publish "src/AiEnterprise.RiskScoring/AiEnterprise.RiskScoring.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AiEnterprise.RiskScoring.dll"]
