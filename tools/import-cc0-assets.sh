#!/usr/bin/env bash
# 내려받은 CC0/OFL 에셋 중 게임에서 쓰는 파일만 골라 client/Assets 로 복사한다 (한 번 실행해 커밋, CI에서는 쓰지 않음)
# 사용: tools/import-cc0-assets.sh <cc0 다운로드 폴더>
set -euo pipefail
SRC="${1:?CC0 다운로드 폴더를 넘겨 주세요}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ICONS="$ROOT/client/Assets/Game/Resources/Icons"
NATURE="$ROOT/client/Assets/ThirdParty/CC0/KenneyNatureKit"
TEX="$ROOT/client/Assets/ThirdParty/CC0/AmbientCG"
mkdir -p "$ICONS" "$NATURE" "$TEX"

# 아이템 템플릿 ID → 아이콘 파일 (OpenGameArt "496 pixel art icons", CC0)
while read -r id file; do
  cp "$SRC/icons/$file" "$ICONS/$id.png"
done <<'MAP'
potion_hp_s P_Red01.png
potion_hp_l P_Red03.png
potion_mp_s P_Blue01.png
scroll_weapon I_Scroll02.png
scroll_armor I_Scroll.png
sword_short W_Sword001.png
sword_long W_Sword010.png
bow_short W_Bow01.png
bow_long W_Bow05.png
staff_oak W_Staff01.png
axe_battle W_Axe005.png
helm_leather C_Elm01.png
helm_iron C_Elm03.png
armor_leather A_Armour01.png
armor_chain A_Armor04.png
armor_plate A_Armor05.png
gloves_leather W_Fist001.png
gloves_iron W_Fist005.png
boots_leather A_Shoes01.png
boots_iron A_Shoes05.png
gold I_GoldCoin.png
ground_bag I_Chest01.png
MAP

# Kenney Nature Kit (CC0) FBX
for m in tree_default tree_oak tree_detailed tree_fat tree_pineRoundA tree_pineTallA tree_cone \
         tree_default_dark tree_oak_dark tree_pineTallA_detailed tree_default_fall tree_oak_fall tree_cone_dark \
         rock_largeA rock_smallA rock_tallA stone_largeA stone_smallA stone_tallA \
         plant_bush plant_bushLarge plant_bushSmall grass grass_large flower_redA mushroom_red mushroom_tanGroup \
         tent_detailedOpen tent_smallClosed campfire_logs fence_simple fence_gate log_stack sign pot_large \
         path_stone statue_obelisk statue_column; do
  cp "$SRC/nature-kit/Models/FBX format/$m.fbx" "$NATURE/$m.fbx"
done
cp "$SRC/nature-kit/License.txt" "$NATURE/License.txt"

# ambientCG (CC0) 지형 텍스처 1K, 1MB 이하로 다시 인코딩
for id in Grass001 Ground048 PavingStones070; do
  tmp="$(mktemp -d)"
  unzip -o -q "$SRC/_zips/${id}_1K-JPG.zip" "${id}_1K-JPG_Color.jpg" -d "$tmp"
  sips -s format jpeg -s formatOptions 70 "$tmp/${id}_1K-JPG_Color.jpg" --out "$TEX/${id}_Color.jpg" >/dev/null
  rm -rf "$tmp"
done
ls -la "$TEX"
