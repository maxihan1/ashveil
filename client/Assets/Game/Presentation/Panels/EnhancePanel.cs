// 강화 창: 장비 선택 → 맞는 주문서·비용 표시 → 서버 판정 결과(성공·실패·파괴) 연출
using System.Linq;
using System.Threading.Tasks;
using Ashveil.Application;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using UnityEngine;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class EnhancePanel : PanelBase
    {
        private const long GoldCostPerLevel = 100; // 표시용. 실제 비용은 서버 설정이 판정한다.
        // 결과 뒤 이 시간 동안 버튼을 잠근다. 서버도 이 안에 온 강화를 COOLDOWN 으로 막는다(두 번 누르기).
        private static readonly int RepeatGuardMs = new EnhanceConfig().RepeatGuardMilliseconds;

        private RectTransform _list;
        private Text _detail;
        private Button _enhanceButton;
        private Text _result;
        private long _selectedId;
        private bool _busy;

        public static void Open(Transform layer) => OpenSingle<EnhancePanel>(layer, "EnhancePanel", "강화 - 대장장이 오른", new Vector2(1300, 860));

        protected override void Build()
        {
            _list = Ui.ScrollList(Body, "EquipmentList", out var scroll);
            // 왼쪽 세로 전체, 폭 560
            var left = Ui.Rect(scroll);
            left.anchorMin = new Vector2(0, 0);
            left.anchorMax = new Vector2(0, 1);
            left.pivot = new Vector2(0, 0.5f);
            left.offsetMin = Vector2.zero;
            left.offsetMax = new Vector2(560, 0);

            var right = Ui.Panel(Body, "DetailPanel", UiColors.PanelLight);
            Ui.Stretch(right.rectTransform, 590, 0, 0, 0);
            _detail = Ui.Label(right.transform, "DetailText", "왼쪽에서 강화할 장비를 고르세요.", 30, TextAnchor.UpperLeft);
            Ui.Stretch(_detail.rectTransform, 30, 30, 30, 260);
            _result = Ui.Label(right.transform, "ResultText", "", 52, TextAnchor.MiddleCenter);
            _result.fontStyle = FontStyle.Bold;
            Ui.Place(_result.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(620, 90));
            _enhanceButton = Ui.Button(right.transform, "EnhanceButton", "강화하기", OnEnhance, UiColors.ButtonAccent, 34);
            Ui.Place(Ui.Rect(_enhanceButton), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(360, 100));
            GameContext.Player.InventoryChanged += Refresh;
        }

        private void OnDestroy()
        {
            if (GameContext.Player != null) GameContext.Player.InventoryChanged -= Refresh;
        }

        public override void Refresh()
        {
            if (_list == null) return;
            Ui.Clear(_list);
            var equipment = GameContext.Player.Items.Where(i => ItemCatalog.Get(i.TemplateId).IsEquipment).ToList();
            foreach (var item in equipment)
            {
                var selected = item.Id == _selectedId;
                var row = Ui.Row(_list, "Equip_" + item.Id, 100, selected ? UiColors.ButtonAccent : UiColors.PanelLight);
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = row;
                var id = item.Id;
                button.onClick.AddListener(() => { _selectedId = id; _result.text = ""; Refresh(); });
                var icon = Ui.Node(row.transform, "Icon").gameObject.AddComponent<RawImage>();
                icon.texture = GameResources.Icon(item.TemplateId);
                icon.raycastTarget = false;
                Ui.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(76, 76));
                var label = Ui.Label(row.transform, "NameText", ItemText.NameOf(item.TemplateId, item.EnhanceLevel) + (item.EquippedSlot != null ? "  [장착]" : ""), 28);
                Ui.Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(100, 0), new Vector2(420, 90));
            }
            RenderDetail();
        }

        private ItemDto Selected => GameContext.Player.Items.FirstOrDefault(i => i.Id == _selectedId);

        private ItemDto ScrollFor(ItemTemplate target)
        {
            var needed = target.Type == ItemType.Weapon ? ItemCatalog.ScrollWeapon : ItemCatalog.ScrollArmor;
            return GameContext.Player.FindItem(needed);
        }

        private void RenderDetail()
        {
            var item = Selected;
            if (item == null)
            {
                _detail.text = "왼쪽에서 강화할 장비를 고르세요.";
                _enhanceButton.interactable = false;
                return;
            }
            var template = ItemCatalog.Get(item.TemplateId);
            var scrollName = template.Type == ItemType.Weapon ? "무기 강화 주문서" : "방어구 강화 주문서";
            var scroll = ScrollFor(template);
            var cost = GoldCostPerLevel * (item.EnhanceLevel + 1);
            _detail.text =
                $"<size=38><color=#FFD24D>{ItemText.NameOf(item.TemplateId, item.EnhanceLevel)}</color></size>\n\n" +
                $"{ItemText.Describe(item)}\n\n" +
                $"필요 주문서: {scrollName} (보유 {(scroll != null ? scroll.Quantity : 0)}장)\n" +
                $"강화 비용: {cost:N0} 골드 (보유 {GameContext.Player.Gold:N0})\n" +
                "<color=#AAB0BB>+3까지는 안전하게 성공합니다. 이후에는 실패하거나 장비가 파괴될 수 있습니다.</color>";
            _enhanceButton.interactable = !_busy && scroll != null;
        }

        private void OnEnhance()
        {
            var item = Selected;
            if (item == null || _busy) return;
            var scroll = ScrollFor(ItemCatalog.Get(item.TemplateId));
            if (scroll == null)
            {
                Toast.Show("맞는 강화 주문서가 없습니다.");
                return;
            }
            _busy = true;
            _enhanceButton.interactable = false;
            _result.text = "...";
            AsyncUi.Run(async () =>
            {
                try
                {
                    var response = await GameContext.Api.EnhanceAsync(item.Id, scroll.Id);
                    GameContext.Player.Apply(response.Inventory);
                    // 장착 장비 강화는 스탯이 바뀌므로 캐릭터 정보를 다시 받는다.
                    if (item.EquippedSlot != null) GameContext.Player.Apply(await GameContext.Api.GetCharacterAsync());
                    if (this != null) ShowResult(response);
                    await Task.Delay(RepeatGuardMs);
                }
                finally
                {
                    _busy = false;
                    if (this != null) RenderDetail();
                }
            }, e =>
            {
                if (this != null) _result.text = "";
                Toast.Show(e.Message);
            });
        }

        private void ShowResult(EnhanceResponse response)
        {
            var player = WorldServices.Player;
            var position = player != null ? player.transform.position : Vector3.zero;
            switch (response.Result)
            {
                case EnhanceResult.Success:
                    _result.text = $"강화 성공! +{response.ToLevel}";
                    _result.color = UiColors.Good;
                    EffectPlayer.Play(EffectPlayer.EnhanceSuccess, position);
                    break;
                case EnhanceResult.Fail:
                    _result.text = "강화 실패 (변화 없음)";
                    _result.color = UiColors.TextDim;
                    EffectPlayer.Play(EffectPlayer.EnhanceFail, position);
                    break;
                default:
                    _result.text = "장비가 파괴되었습니다!";
                    _result.color = UiColors.Bad;
                    _selectedId = 0;
                    EffectPlayer.Play(EffectPlayer.EnhanceDestroy, position);
                    break;
            }
            Toast.Show(_result.text);
        }
    }
}
