// QA 빌드 전용 GM 콘솔: 골드·아이템·레벨·순간이동·강화 확률·봇 리셋·성향치·드랍 확률 (서버 GM API 호출)
#if QA_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using Ashveil.Application;
using Ashveil.Domain;
using Ashveil.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class GmConsole : PanelBase
    {
        private RectTransform _list;
        private Text _output;

        public static void Open(Transform layer) => OpenSingle<GmConsole>(layer, "GmConsole", "GM 콘솔 (QA 전용)", new Vector2(1500, 960));

        protected override void Build()
        {
            _list = Ui.ScrollList(Body, "GmList", out var scroll);
            Ui.Stretch(Ui.Rect(scroll), 0, 0, 0, 110);
            _output = Ui.Label(Body, "GmOutputText", "", 24, TextAnchor.UpperLeft, UiColors.TextDim);
            Ui.Place(_output.rectTransform, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(1440, 100));

            var gold = Section("GmGoldSection", "골드 지급(음수면 차감)");
            var goldInput = Field(gold, "GmGoldInput", "10000", 16);
            Action(gold, "GmGoldButton", "지급", 0, () => Run(async () =>
            {
                var c = await GameContext.Api.GmGoldAsync(Long(goldInput));
                GameContext.Player.Apply(c);
                Print($"골드 {c.Gold:N0}");
            }));

            var item = Section("GmItemSection", "아이템 지급 (ID · 수량 · 강화)");
            var itemId = Field(item, "GmItemIdInput", ItemCatalog.ScrollWeapon, 16, 330);
            var itemQty = Field(item, "GmItemQtyInput", "10", 362, 120);
            var itemEnhance = Field(item, "GmItemEnhanceInput", "0", 498, 120);
            Action(item, "GmItemButton", "지급", 0, () => Run(async () =>
            {
                var inventory = await GameContext.Api.GmItemAsync(itemId.text.Trim(), Int(itemQty), Int(itemEnhance));
                GameContext.Player.Apply(inventory);
                Print($"{ItemText.NameOf(itemId.text.Trim())} 지급, 인벤토리 {inventory.Used}/{inventory.Capacity}");
            }));
            var ids = new List<string>();
            foreach (var t in ItemCatalog.All) ids.Add(t.Id);
            Hint(item, "ID 목록: " + string.Join(", ", ids));

            var level = Section("GmLevelSection", "레벨 설정");
            var levelInput = Field(level, "GmLevelInput", "10", 16);
            Action(level, "GmLevelButton", "설정", 0, () => Run(async () =>
            {
                var c = await GameContext.Api.GmLevelAsync(Int(levelInput));
                GameContext.Player.Apply(c, fillVitals: true);
                Print($"레벨 {c.Level}");
            }));

            var teleport = Section("GmTeleportSection", "순간이동");
            var zones = new[] { ZoneId.Town, ZoneId.Beginner, ZoneId.Intermediate, ZoneId.Boss };
            for (var i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                Action(teleport, "Teleport" + zone, ZoneMap.DisplayName(zone), zones.Length - 1 - i, () =>
                {
                    var (x, z) = ZoneMap.CenterOf(zone);
                    WorldServices.Player.TeleportTo(new Vec2(x, z));
                    Print($"{ZoneMap.DisplayName(zone)} ({x}, {z})로 이동");
                }, 250);
            }

            var rate = Section("GmEnhanceRateSection", "강화 확률 덮어쓰기 (목표 수치 · 성공 · 실패 · 파괴)");
            var rateLevel = Field(rate, "GmEnhanceTargetInput", "4", 16, 110);
            var rateSuccess = Field(rate, "GmEnhanceSuccessInput", "0", 142, 130);
            var rateFail = Field(rate, "GmEnhanceFailInput", "0", 288, 130);
            var rateDestroy = Field(rate, "GmEnhanceDestroyInput", "1", 434, 130);
            Action(rate, "GmEnhanceRateResetButton", "초기화", 0, () => Run(async () =>
            {
                await GameContext.Api.GmEnhanceRateAsync(null, true);
                Print("강화 확률을 기본값으로 되돌렸습니다.");
            }));
            Action(rate, "GmEnhanceRateButton", "적용", 1, () => Run(async () =>
            {
                var config = await GameContext.Api.GmEnhanceRateAsync(new List<EnhanceRate>
                {
                    new EnhanceRate { TargetLevel = Int(rateLevel), Success = Double(rateSuccess), Fail = Double(rateFail), Destroy = Double(rateDestroy) },
                }, false);
                Print($"+{Int(rateLevel)} 확률 적용 (정의된 행 {config.Rates.Count}개)");
            }));

            var bots = Section("GmBotSection", "봇 리셋 (삭제 후 다시 생성)");
            Action(bots, "GmBotResetAllButton", "전체", 0, () => Run(async () =>
            {
                var result = await GameContext.Api.GmBotResetAsync(null);
                WorldServices.Bots.RefreshSoon();
                Print($"봇 {result.BotCount}명 생성");
            }));
            Action(bots, "GmBotResetZoneButton", "현재 구역", 1, () => Run(async () =>
            {
                var zone = WorldGround.ZoneAt(WorldServices.Player.transform.position);
                var result = await GameContext.Api.GmBotResetAsync(zone);
                WorldServices.Bots.RefreshSoon();
                Print($"{ZoneMap.DisplayName(zone)} 봇 {result.BotCount}명 생성");
            }));

            var alignment = Section("GmAlignmentSection", "성향치 설정 (-1000 ~ 1000, 음수면 악명)");
            var alignmentInput = Field(alignment, "GmAlignmentInput", "-500", 16);
            Action(alignment, "GmAlignmentButton", "설정", 0, () => Run(async () =>
            {
                var c = await GameContext.Api.GmAlignmentAsync(Int(alignmentInput));
                GameContext.Player.Apply(c);
                Print($"성향치 {c.Alignment.Value} ({AlignmentView.Label(c.Alignment.State)})");
            }));

            var drop = Section("GmDropSection", "드랍 확률 (봇 전리품 · 악명 봇 전리품 · 사망 드랍 전 구간)");
            var botLoot = Field(drop, "GmBotLootInput", "1", 16, 130);
            var botLootChaotic = Field(drop, "GmBotLootChaoticInput", "1", 162, 130);
            var deathDrop = Field(drop, "GmDeathDropInput", "1", 308, 130);
            Action(drop, "GmDropResetButton", "초기화", 0, () => Run(async () =>
            {
                await GameContext.Api.GmDropRateAsync(null, null, null, true);
                Print("드랍 확률을 기본값으로 되돌렸습니다.");
            }));
            Action(drop, "GmDropButton", "적용", 1, () => Run(async () =>
            {
                var chance = Double(deathDrop);
                var tiers = new List<DeathDropTier>
                {
                    new DeathDropTier { MaxAlignment = -300, Chance = chance, IncludeEquipped = true },
                    new DeathDropTier { MaxAlignment = 1000, Chance = chance, IncludeEquipped = false },
                };
                var config = await GameContext.Api.GmDropRateAsync(Double(botLoot), Double(botLootChaotic), tiers, false);
                Print($"봇 전리품 {config.BotLootChance:P0}, 악명 봇 {config.BotLootChanceChaotic:P0}, 사망 드랍 {chance:P0}");
            }));

            var info = Section("GmConfigSection", "현재 서버 설정");
            Action(info, "GmConfigButton", "조회", 0, () => Run(async () =>
            {
                var config = await GameContext.Api.GmConfigAsync();
                var rates = new List<string>();
                foreach (var r in config.Enhance.Rates) rates.Add($"+{r.TargetLevel} {r.Success:0.##}/{r.Fail:0.##}/{r.Destroy:0.##}");
                Print($"안전 +{config.Enhance.SafeLevel} | {string.Join("  ", rates)}\n" +
                      $"봇 전리품 {config.Drop.BotLootChance:P0} / 악명 {config.Drop.BotLootChanceChaotic:P0}, 바닥 유지 {config.Drop.GroundItemLifetimeSeconds}초");
            }));
        }

        private RectTransform Section(string name, string title)
        {
            var row = Ui.Row(_list, name, 130);
            var label = Ui.Label(row.transform, "Title", title, 26, TextAnchor.UpperLeft, UiColors.Gold);
            Ui.Place(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -6), new Vector2(1400, 36));
            return row.rectTransform;
        }

        private static InputField Field(RectTransform section, string name, string value, float x, float width = 220)
        {
            var input = Ui.Input(section, name, "", false, 26);
            input.text = value;
            Ui.Place(Ui.Rect(input), new Vector2(0, 0), new Vector2(0, 0), new Vector2(x, 12), new Vector2(width, 64));
            return input;
        }

        private static void Hint(RectTransform section, string text)
        {
            var label = Ui.Label(section, "Hint", text, 18, TextAnchor.LowerLeft, UiColors.TextDim);
            Ui.Place(label.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-340, 8), new Vector2(560, 90));
        }

        private static void Action(RectTransform section, string name, string label, int slot, System.Action onClick, float width = 180)
        {
            var button = Ui.Button(section, name, label, onClick, UiColors.ButtonAccent, 26);
            Ui.Place(Ui.Rect(button), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-16 - slot * (width + 12), 12), new Vector2(width, 64));
        }

        private void Run(Func<System.Threading.Tasks.Task> action) =>
            AsyncUi.Run(action, e => Print($"<color=#FF7070>[{e.Code}] {e.Message}</color>"));

        private void Print(string message)
        {
            if (_output != null) _output.text = message;
            Debug.Log("[GM] " + message);
        }

        private static int Int(InputField f) => int.TryParse(f.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        private static long Long(InputField f) => long.TryParse(f.text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
        private static double Double(InputField f) => double.TryParse(f.text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
#endif
