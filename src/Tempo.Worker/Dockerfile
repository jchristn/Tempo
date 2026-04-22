#
# Build from the repository root
#   docker build -f src/Tempo.Worker/Dockerfile .
#

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo

COPY src/Tempo/Tempo.csproj src/Tempo/
COPY src/Tempo.Core/Tempo.Core.csproj src/Tempo.Core/
COPY src/Tempo.Worker/Tempo.Worker.csproj src/Tempo.Worker/
COPY README.md LICENSE.md ./
COPY assets ./assets

RUN dotnet restore src/Tempo.Worker/Tempo.Worker.csproj

COPY src ./src

RUN dotnet publish src/Tempo.Worker/Tempo.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false /p:GeneratePackageOnBuild=false

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends bash curl nodejs python3 ca-certificates \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish ./

RUN mkdir -p /var/lib/tempo-worker/config /var/lib/tempo-worker/logs /var/lib/tempo-worker/runtime-cache /var/lib/tempo-worker/scratch
COPY docker/tempo.worker.json /var/lib/tempo-worker/config/tempo.worker.json
COPY docker/tempo.worker.entrypoint.sh /usr/local/bin/tempo-worker-entrypoint.sh
RUN chmod +x /usr/local/bin/tempo-worker-entrypoint.sh

ENTRYPOINT ["/usr/local/bin/tempo-worker-entrypoint.sh"]
