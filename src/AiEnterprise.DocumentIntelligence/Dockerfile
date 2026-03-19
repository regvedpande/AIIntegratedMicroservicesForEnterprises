FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5002

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/AiEnterprise.DocumentIntelligence/AiEnterprise.DocumentIntelligence.csproj", "src/AiEnterprise.DocumentIntelligence/"]
COPY ["src/AiEnterprise.Core/AiEnterprise.Core.csproj", "src/AiEnterprise.Core/"]
COPY ["src/AiEnterprise.Infrastructure/AiEnterprise.Infrastructure.csproj", "src/AiEnterprise.Infrastructure/"]
COPY ["src/AiEnterprise.Shared/AiEnterprise.Shared.csproj", "src/AiEnterprise.Shared/"]

RUN dotnet restore "src/AiEnterprise.DocumentIntelligence/AiEnterprise.DocumentIntelligence.csproj"
COPY . .
RUN dotnet publish "src/AiEnterprise.DocumentIntelligence/AiEnterprise.DocumentIntelligence.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AiEnterprise.DocumentIntelligence.dll"]
