#!/usr/bin/env bash
# git-lfs 를 설치하지 않은 Mac 에서 Ubuntu 컨테이너로 tools/fetch-bossroom.sh 를 실행한다
# 사용: BOSSROOM_CACHE_HOST=<받은 저장소를 둘 Mac 경로> tools/fetch-bossroom-docker.sh
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CACHE="${BOSSROOM_CACHE_HOST:-$HOME/.cache/ashveil-bossroom}"
mkdir -p "$CACHE"
# 작업 디렉터리를 / 로 두어, 컨테이너 안에서 깨진 worktree .git 파일을 git 이 탐색하지 않게 한다.
exec docker run --rm -v "$ROOT":/repo -v "$CACHE":/cache -w / ubuntu:24.04 bash -c '
  apt-get update -qq >/dev/null &&
  apt-get install -y -qq git git-lfs rsync ca-certificates >/dev/null 2>&1 &&
  BOSSROOM_CACHE=/cache bash /repo/tools/fetch-bossroom.sh'
