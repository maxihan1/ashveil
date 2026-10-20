#!/usr/bin/env bash
# Boss Room 샘플(고정 커밋)에서 아트 폴더만 sparse + LFS 로 받아 client/Assets/ThirdParty/BossRoom 에 복사한다
# 사용: tools/fetch-bossroom.sh            (git-lfs 필요. GitHub Actions 러너에는 기본 설치돼 있다)
#       BOSSROOM_CACHE=/path tools/fetch-bossroom.sh   (받은 저장소를 재사용할 위치)
set -euo pipefail

COMMIT="1299ba4fc97d83b634e7d32a86c438ba668d7f9c"   # 2025-10-23, Unity 6000.0.52f1
REPO="https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop.git"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DEST="$ROOT/client/Assets/ThirdParty/BossRoom"
WORK="${BOSSROOM_CACHE:-$ROOT/.cache/bossroom}"

# 아트만 가져온다. Scripts, Netcode·UGS 관련 프리팹 폴더(Prefabs/Character 등)는 받지 않는다.
FOLDERS=(
  "Assets/Models"
  "Assets/Material"
  "Assets/Textures/Characters"
  "Assets/Textures/Environment"
  "Assets/Shaders"
  "Assets/Prefabs/CharGFX"
  "Assets/Prefabs/Dungeon"
  "Assets/VFX"
)

command -v git-lfs >/dev/null || { echo "git-lfs 가 필요합니다 (brew install git-lfs / apt install git-lfs)"; exit 1; }

if [[ ! -d "$WORK/.git" ]]; then
  mkdir -p "$WORK"
  git -C "$WORK" init -q
  git -C "$WORK" remote add origin "$REPO"
fi
git -C "$WORK" sparse-checkout set "${FOLDERS[@]}"
if [[ "$(git -C "$WORK" rev-parse -q --verify HEAD 2>/dev/null || true)" != "$COMMIT" ]]; then
  git -C "$WORK" fetch -q --depth 1 --filter=blob:none origin "$COMMIT"
  GIT_LFS_SKIP_SMUDGE=1 git -C "$WORK" checkout -q --force FETCH_HEAD
fi
INCLUDE="$(printf '%s/**,' "${FOLDERS[@]}")"
git -C "$WORK" lfs install --local >/dev/null
git -C "$WORK" lfs pull --include="${INCLUDE%,}"

mkdir -p "$DEST"
for folder in "${FOLDERS[@]}"; do
  rel="${folder#Assets/}"
  mkdir -p "$DEST/$(dirname "$rel")"
  rsync -a --delete "$WORK/$folder/" "$DEST/$rel/"
  [[ -f "$WORK/$folder.meta" ]] && cp "$WORK/$folder.meta" "$DEST/$rel.meta"
done

# LFS 포인터가 아니라 실제 파일이 왔는지 확인한다.
FBX="$DEST/Models/CharacterSet.fbx"
if [[ ! -f "$FBX" ]] || head -c 100 "$FBX" | grep -q "git-lfs.github.com"; then
  echo "Boss Room LFS 파일을 받지 못했습니다: $FBX"; exit 1
fi
echo "Boss Room 아트 복사 완료 → $DEST ($(du -sh "$DEST" | cut -f1))"
