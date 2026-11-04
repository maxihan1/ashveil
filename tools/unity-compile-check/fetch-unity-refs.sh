#!/usr/bin/env bash
# Unity 리눅스 에디터 배포본에서 컴파일 검사에 필요한 관리 DLL·내장 패키지만 스트리밍으로 추출한다 (Editor 설치·실행 없음)
# 사용: UNITY_REF_DIR=/path tools/unity-compile-check/fetch-unity-refs.sh [version] [changeset]
set -euo pipefail
VERSION="${1:-6000.0.84f1}"
CHANGESET="${2:-78ab6fc243d5}"
DEST="${UNITY_REF_DIR:-$(cd "$(dirname "$0")" && pwd)/.cache/$VERSION}"
URL="https://download.unity3d.com/download_unity/$CHANGESET/LinuxEditorInstaller/Unity-$VERSION.tar.xz"
PATTERNS=(
  'Editor/Data/Managed/*'
  'Editor/Data/NetStandard/*'
  'Editor/Data/Resources/PackageManager/BuiltInPackages/*'
)

if [[ -f "$DEST/.complete" ]]; then echo "이미 있음: $DEST"; exit 0; fi
mkdir -p "$DEST"
echo "다운로드·추출: $URL -> $DEST"
if tar --version 2>/dev/null | grep -q "GNU tar"; then
  # GNU tar (CI 러너): 패턴 인자 + --wildcards
  curl -fsSL "$URL" | tar -xJf - -C "$DEST" --wildcards "${PATTERNS[@]}"
else
  # bsdtar (macOS)
  INCLUDES=()
  for p in "${PATTERNS[@]}"; do INCLUDES+=(--include "$p"); done
  curl -fsSL "$URL" | tar -xJf - -C "$DEST" "${INCLUDES[@]}"
fi
touch "$DEST/.complete"
du -sh "$DEST"
