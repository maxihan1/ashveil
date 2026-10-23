// 인벤토리 창(40칸 격자)과 아이템 상세 팝업(장착·해제·사용)
using Ashveil.Application;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using UnityEngine;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class InventoryPanel : PanelBase
    {
        private RectTransform _grid;
        private Text _summary;

        public static void Open(Transform layer) => OpenSingle<InventoryPanel>(layer, "InventoryPanel", "가방", new Vector2(1240, 860));

        protected override void Build()
        {
            _summary = Ui.Label(Body, "SummaryText", "", 28, TextAnchor.MiddleLeft, UiColors.TextDim);
            Ui.Place(_summary.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(1100, 50));
            _grid = Ui.Place(Ui.Node(Body, "Grid"), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -60), new Vector2(1190, 680));
            var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(136, 128);
            grid.spacing = new Vector2(12, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 8;
            GameContext.Player.InventoryChanged += Refresh;
        }

        private void OnDestroy()
        {
            if (GameContext.Player != null) GameContext.Player.InventoryChanged -= Refresh;
        }

        public override void Refresh()
        {
            if (_grid == null) return;
            var inventory = GameContext.Player.Inventory;
            var items = inventory?.Items ?? new System.Collections.Generic.List<ItemDto>();
            _summary.text = $"{items.Count} / {InventoryRules.Capacity}칸   보유 골드 {GameContext.Player.Gold:N0}";
            Ui.Clear(_grid);
            for (var i = 0; i < InventoryRules.Capacity; i++)
            {
                if (i >= items.Count)
                {
                    Ui.Panel(_grid, "Slot" + i, new Color(1, 1, 1, 0.05f));
                    continue;
                }
                var item = items[i]; // for 변수는 클로저가 공유하므로 지역 변수로 복사한다
                ItemSlot(_grid, "Slot" + i, item, () => ItemDetailPopup.Show(transform.parent, item));
            }
        }

        public static Button ItemSlot(Transform parent, string name, ItemDto item, System.Action onClick)
        {
            var button = Ui.Button(parent, name, "", onClick, new Color(1, 1, 1, 0.1f));
            var icon = Ui.Node(button.transform, "Icon").gameObject.AddComponent<RawImage>();
            icon.texture = GameResources.Icon(item.TemplateId);
            icon.raycastTarget = false;
            Ui.Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(84, 84));
            if (item.Quantity > 1)
            {
                var qty = Ui.Label(button.transform, "Quantity", item.Quantity.ToString(), 24, TextAnchor.LowerRight);
                Ui.Stretch(qty.rectTransform, 4, 8, 4, 4);
            }
            if (item.EnhanceLevel > 0)
            {
                var enhance = Ui.Label(button.transform, "Enhance", "+" + item.EnhanceLevel, 24, TextAnchor.UpperLeft, UiColors.Gold);
                Ui.Stretch(enhance.rectTransform, 8, 4, 4, 4);
            }
            if (item.EquippedSlot != null)
            {
                var equipped = Ui.Label(button.transform, "Equipped", "E", 24, TextAnchor.UpperRight, UiColors.Good);
                Ui.Stretch(equipped.rectTransform, 4, 8, 4, 4);
            }
            return button;
        }
    }

    public static class ItemDetailPopup
    {
        public static void Show(Transform layer, ItemDto item)
        {
            var existing = layer.Find("ItemDetailPopup");
            if (existing != null) Object.Destroy(existing.gameObject);
            var window = Ui.Modal(layer, "ItemDetailPopup", new Vector2(760, 720), out var backdrop);
            void Close() => Object.Destroy(backdrop.gameObject);

            var icon = Ui.Node(window, "Icon").gameObject.AddComponent<RawImage>();
            icon.texture = GameResources.Icon(item.TemplateId);
            Ui.Place(icon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -30), new Vector2(120, 120));
            var name = Ui.Label(window, "ItemName", ItemText.NameOf(item.TemplateId, item.EnhanceLevel), 38, TextAnchor.MiddleLeft, UiColors.Gold);
            Ui.Place(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(170, -40), new Vector2(560, 100));
            var description = Ui.Label(window, "ItemDescription", ItemText.Describe(item), 30, TextAnchor.UpperLeft);
            Ui.Place(description.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -180), new Vector2(680, 380));

            var template = ItemCatalog.Get(item.TemplateId);
            if (template.IsEquipment)
            {
                var equipped = item.EquippedSlot != null;
                var action = Ui.Button(window, equipped ? "UnequipButton" : "EquipButton", equipped ? "해제" : "장착", () =>
                    AsyncUi.Run(async () =>
                    {
                        var result = equipped ? await GameContext.Api.UnequipAsync(item.Id) : await GameContext.Api.EquipAsync(item.Id);
                        GameContext.Player.Apply(result.Inventory);
                        GameContext.Player.Apply(result.Stats);
                        Toast.Show(equipped ? "장비를 해제했습니다." : "장비를 장착했습니다.");
                        Close();
                    }), UiColors.ButtonAccent);
                Ui.Place(Ui.Rect(action), new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-15, 30), new Vector2(260, 90));
            }
            else if (template.Type == ItemType.Consumable)
            {
                var use = Ui.Button(window, "UseButton", "사용", () =>
                    AsyncUi.Run(async () =>
                    {
                        var result = await GameContext.Api.UseItemAsync(item.Id);
                        GameContext.Player.Apply(result.Inventory);
                        GameContext.Player.Heal(result.HealHp, result.HealMp);
                        Toast.Show(template.Name + "을(를) 사용했습니다.");
                        Close();
                    }), UiColors.ButtonAccent);
                Ui.Place(Ui.Rect(use), new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-15, 30), new Vector2(260, 90));
            }
            var close = Ui.Button(window, "CloseButton", "닫기", Close);
            Ui.Place(Ui.Rect(close), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(15, 30), new Vector2(260, 90));
        }
    }
}
