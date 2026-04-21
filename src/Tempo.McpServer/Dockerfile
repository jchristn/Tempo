#
# Build from the repository root
#   docker build -f src/Tempo.McpServer/Dockerfile .
#

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/Tempo/Tempo.csproj src/Tempo/
COPY src/Tempo.Core/Tempo.Core.csproj src/Tempo.Core/
COPY src/Tempo.McpServer/Tempo.McpServer.csproj src/Tempo.McpServer/
COPY README.md LICENSE.md ./
COPY assets ./assets

RUN dotnet restore src/Tempo.McpServer/Tempo.McpServer.csproj

COPY src ./src

RUN dotnet publish src/Tempo.McpServer/Tempo.McpServer.csproj -c Release -o /app/publish /p:UseAppHost=false /p:GeneratePackageOnBuild=false

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./

RUN mkdir -p /var/lib/tempo-mcp/config
COPY docker/tempo.mcp.json /var/lib/tempo-mcp/config/tempo.mcp.json

EXPOSE 8910 8911 8912

ENTRYPOINT ["dotnet", "Tempo.McpServer.dll", "--config", "/var/lib/tempo-mcp/config/tempo.mcp.json"]
