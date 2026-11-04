#!/usr/bin/env bash
# 로컬에 .NET SDK 없이 Docker 컨테이너로 dotnet 명령을 실행하는 래퍼
# 사용: tools/dotnet-docker.sh test server/Ashveil.slnx
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
NETWORK_ARGS=()
if [[ -n "${DOCKER_NETWORK:-}" ]]; then NETWORK_ARGS=(--network "$DOCKER_NETWORK"); fi
exec docker run --rm ${NETWORK_ARGS[@]+"${NETWORK_ARGS[@]}"} \
  -v "$ROOT":/src -w /src \
  -v ashveil-nuget:/root/.nuget/packages \
  -e ConnectionStrings__TestAdmin="${TEST_CONNECTION:-Host=ashveil-test-db;Port=5432;Username=postgres;Password=postgres}" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e DOTNET_NOLOGO=1 \
  mcr.microsoft.com/dotnet/sdk:10.0 dotnet "$@"
