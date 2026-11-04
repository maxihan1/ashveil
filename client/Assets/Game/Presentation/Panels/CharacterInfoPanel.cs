// 캐릭터 정보 창: 스탯·성향 / PK 기록(킬·데스·성향 변동) / 골드 원장 탭, 그리고 사망 창
using Ashveil.Application;
using Ashveil.Domain;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using UnityEngine;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class CharacterInfoPanel : PanelBase
    {
        private RectTransform _list;
        private int _tab;

        public static void Open(Transform layer) => OpenSingle<CharacterInfoPanel>(layer, "CharacterInfoPanel", "캐릭터 정보", new Vector2(1240, 880));

        protected override void Build()
        {
            var tabs = new Tabs(Body, "InfoTabs", Vector2.zero, 780);
            tabs.Add("StatsTab", "정보", () => { _tab = 0; Refresh(); });
            tabs.Add("PkTab", "PK 기록", () => { _tab = 1; Refresh(); });
            tabs.Add("LedgerTab", "골드 내역", () => { _tab = 2; Refresh(); });
            _list = Ui.ScrollList(Body, "InfoList", out var scroll);
            Ui.Stretch(Ui.Rect(scroll), 0, 0, 90, 0);
            tabs.Select(0);
        }

        public override void Refresh()
        {
            if (_list == null) return;
            Ui.Clear(_list);
            switch (_tab)
            {
                case 0: RenderStats(); break;
                case 1: LoadPkHistory(); break;
                default: LoadLedger(); break;
            }
        }

        private void Line(string name, string text, int size = 30, float height = 64)
        {
            var row = Ui.Row(_list, name, height);
            var label = Ui.Label(row.transform, "Text", text, size);
            Ui.Stretch(label.rectTransform, 20, 20, 4, 4);
        }

        private void RenderStats()
        {
            var c = GameContext.Player.Character;
            if (c == null) return;
            var state = AlignmentRules.GetState(c.Alignment.Value, c.Alignment.PurpleUntil, System.DateTime.UtcNow);
            Line("NameLine", $"{c.Name}  ·  {AlignmentView.ClassLabel(c.ClassType)}  ·  Lv.{c.Level}", 34);
            Line("ExpLine", $"경험치 {c.Exp:N0} / {c.ExpToNext:N0}");
            Line("GoldLine", $"골드 {c.Gold:N0}");
            Line("HpLine", $"HP {GameContext.Player.Hp} / {c.Stats.MaxHp}    MP {GameContext.Player.Mp} / {c.Stats.MaxMp}");
            Line("AttackLine", $"공격력 {c.Stats.Attack}    방어력 {c.Stats.Defense}");
            Line("AlignmentLine", $"성향치 {c.Alignment.Value}  <color={AlignmentView.ColorHex(state)}>{AlignmentView.Label(state)}</color>");
            Line("PkLine", $"PK 처치 {c.PkKills}    PK 사망 {c.PkDeaths}");
            Line("ZoneLine", $"현재 위치 {ZoneMap.DisplayName(c.Zone)}");
        }

        private void LoadPkHistory()
        {
            AsyncUi.Run(async () =>
            {
                var history = await GameContext.Api.GetPkHistoryAsync();
                if (this == null || _tab != 1) return;
                Ui.Clear(_list);
                var state = history.Alignment.State;
                Line("PkSummary", $"처치 {history.Kills}  ·  사망 {history.Deaths}  ·  성향치 {history.Alignment.Value} " +
                                  $"<color={AlignmentView.ColorHex(state)}>{AlignmentView.Label(state)}</color>", 32);
                if (history.Records.Count == 0) Line("PkEmpty", "PK 기록이 없습니다.", 28);
                foreach (var r in history.Records)
                {
                    var innocent = r.VictimWasInnocent ? "<color=#FF6060>선량 대상</color>" : "패널티 없음";
                    Line("PkRecord", $"{Local(r.CreatedAt)}  {r.KillerName} → {r.VictimName}  ({innocent}, 성향 {r.AlignmentDelta:+#;-#;0})", 26);
                }
                if (history.AlignmentHistory.Count > 0) Line("AlignmentHeader", "<color=#FFD24D>성향치 변동</color>", 28);
                foreach (var h in history.AlignmentHistory)
                    Line("AlignmentRecord", $"{Local(h.CreatedAt)}  {h.Delta:+#;-#;0} → {h.After}  ({h.Reason})", 26);
            });
        }

        private void LoadLedger()
        {
            AsyncUi.Run(async () =>
            {
                var entries = await GameContext.Api.GetLedgerAsync(100);
                if (this == null || _tab != 2) return;
                Ui.Clear(_list);
                if (entries.Count == 0) Line("LedgerEmpty", "골드 변동 내역이 없습니다.", 28);
                foreach (var e in entries)
                {
                    var color = e.Delta >= 0 ? "#6EE07A" : "#FF7070";
                    Line("LedgerEntry", $"{Local(e.CreatedAt)}  {ItemText.LedgerReason(e.Reason)}  <color={color}>{e.Delta:+#,0;-#,0;0}</color>  잔액 {e.BalanceAfter:N0}", 26);
                }
            });
        }

        private static string Local(System.DateTime utc) => utc.ToLocalTime().ToString("MM-dd HH:mm:ss");
    }

    public static class DeathPanel
    {
        public static void Show(Transform layer, string killerName, long expLost, GroundItemDto dropped)
        {
            var existing = layer.Find("DeathPanel");
            if (existing != null) Object.Destroy(existing.gameObject);
            var window = Ui.Modal(layer, "DeathPanel", new Vector2(900, 560), out var backdrop);
            var title = Ui.Label(window, "Title", "사망했습니다", 56, TextAnchor.MiddleCenter, UiColors.Bad);
            Ui.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(800, 90));
            var lines = $"{killerName}에게 쓰러졌습니다.\n경험치 -{expLost:N0}";
            if (dropped != null) lines += $"\n<color=#FF7070>{ItemText.NameOf(dropped.TemplateId, dropped.EnhanceLevel, dropped.Quantity)}을(를) 떨어뜨렸습니다.</color>";
            var text = Ui.Label(window, "DeathText", lines, 32, TextAnchor.MiddleCenter);
            Ui.Place(text.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(820, 220));
            var respawn = Ui.Button(window, "RespawnButton", "마을에서 부활", () =>
            {
                WorldServices.Player?.RespawnInTown();
                Object.Destroy(backdrop.gameObject);
            }, UiColors.ButtonAccent, 34);
            Ui.Place(Ui.Rect(respawn), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(400, 100));
        }
    }
}
