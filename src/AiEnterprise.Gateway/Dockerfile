FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/AiEnterprise.Gateway/AiEnterprise.Gateway.csproj", "src/AiEnterprise.Gateway/"]
COPY ["src/AiEnterprise.Core/AiEnterprise.Core.csproj", "src/AiEnterprise.Core/"]
COPY ["src/AiEnterprise.Shared/AiEnterprise.Shared.csproj", "src/AiEnterprise.Shared/"]

RUN dotnet restore "src/AiEnterprise.Gateway/AiEnterprise.Gateway.csproj"

COPY . .
RUN dotnet build "src/AiEnterprise.Gateway/AiEnterprise.Gateway.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "src/AiEnterprise.Gateway/AiEnterprise.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
# Run as non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AiEnterprise.Gateway.dll"]
