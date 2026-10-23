#!/usr/bin/env bash
# Docker 안에서 dotnet-ef 로 EF Core 마이그레이션을 추가한다
# 사용: tools/ef-migration.sh <MigrationName>
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
NAME="${1:?마이그레이션 이름을 넘겨 주세요}"
exec docker run --rm -v "$ROOT":/src -w /src/server/src/Ashveil.Server \
  -v ashveil-nuget:/root/.nuget/packages \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
  mcr.microsoft.com/dotnet/sdk:10.0 bash -c "
    dotnet tool install --global dotnet-ef --version 10.0.13 >/dev/null &&
    /root/.dotnet/tools/dotnet-ef migrations add '$NAME' --output-dir Data/Migrations"
