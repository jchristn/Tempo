#
# Build from the repository root
#   docker build -f src/Tempo.Server/Dockerfile .
#

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/Tempo/Tempo.csproj src/Tempo/
COPY src/Tempo.Core/Tempo.Core.csproj src/Tempo.Core/
COPY src/Tempo.Sample.DotnetProcess/Tempo.Sample.DotnetProcess.csproj src/Tempo.Sample.DotnetProcess/
COPY src/Tempo.Server/Tempo.Server.csproj src/Tempo.Server/
COPY README.md LICENSE.md ./
COPY assets ./assets

RUN dotnet restore src/Tempo.Server/Tempo.Server.csproj

COPY src ./src

RUN dotnet publish src/Tempo.Server/Tempo.Server.csproj -c Release -o /app/publish /p:UseAppHost=false /p:GeneratePackageOnBuild=false

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl nodejs python3 ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish ./

RUN mkdir -p /var/lib/tempo-server/config /var/lib/tempo-server/db /var/lib/tempo-server/logs /var/lib/tempo-server/runtime-cache /var/lib/tempo-server/scratch
COPY docker/tempo.server.json /var/lib/tempo-server/config/tempo.json

EXPOSE 8901

ENTRYPOINT ["dotnet", "Tempo.Server.dll", "--config", "/var/lib/tempo-server/config/tempo.json"]
