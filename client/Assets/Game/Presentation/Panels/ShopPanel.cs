// 상점 창: 구매 탭(서버 가격, 악명 할증 표시)과 판매 탭(장착하지 않은 아이템)
using System.Linq;
using Ashveil.Application;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using UnityEngine;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class ShopPanel : PanelBase
    {
        private RectTransform _list;
        private Text _info;
        private bool _sellTab;
        private ShopItemsResponse _prices;

        public static void Open(Transform layer) => OpenSingle<ShopPanel>(layer, "ShopPanel", "상점 - 상인 브릭", new Vector2(1240, 880));

        protected override void Build()
        {
            var tabs = new Tabs(Body, "ShopTabs", Vector2.zero, 520);
            tabs.Add("BuyTab", "구매", () => { _sellTab = false; Render(); });
            tabs.Add("SellTab", "판매", () => { _sellTab = true; Render(); });
            _info = Ui.Label(Body, "InfoText", "", 26, TextAnchor.MiddleRight, UiColors.TextDim);
            Ui.Place(_info.rectTransform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(640, 70));
            _list = Ui.ScrollList(Body, "ShopList", out var scroll);
            Ui.Stretch(Ui.Rect(scroll), 0, 0, 90, 0);
            GameContext.Player.InventoryChanged += Render;
            tabs.Select(0);
            LoadPrices();
        }

        private void OnDestroy()
        {
            if (GameContext.Player != null) GameContext.Player.InventoryChanged -= Render;
        }

        private void LoadPrices()
        {
            AsyncUi.Run(async () =>
            {
                var prices = await GameContext.Api.GetShopItemsAsync();
                if (this == null) return;
                _prices = prices;
                Render();
            });
        }

        public override void Refresh() => LoadPrices();

        private void Render()
        {
            if (_list == null) return;
            Ui.Clear(_list);
            var gold = GameContext.Player.Gold;
            var markup = _prices != null && _prices.PriceMultiplier > 1.0 ? $"   <color=#FF6060>악명 할증 x{_prices.PriceMultiplier:0.0}</color>" : "";
            _info.text = $"보유 골드 {gold:N0}{markup}";
            if (_sellTab) RenderSell();
            else RenderBuy();
        }

        private void RenderBuy()
        {
            if (_prices == null)
            {
                Ui.Label(Ui.Row(_list, "Loading", 80).transform, "Text", "상품 목록을 불러오는 중...", 28, TextAnchor.MiddleCenter);
                return;
            }
            foreach (var entry in _prices.Items)
            {
                var template = ItemCatalog.Get(entry.TemplateId);
                var row = Row(entry.TemplateId, ItemText.NameOf(template.Id), $"{entry.Price:N0} 골드", "Buy_" + template.Id);
                if (template.Stackable)
                {
                    ActionButton(row, "Buy1Button", "1개", 0, () => Buy(template, 1));
                    ActionButton(row, "Buy10Button", "10개", 1, () => Buy(template, 10));
                }
                else ActionButton(row, "BuyButton", "구매", 0, () => Buy(template, 1));
            }
        }

        private void RenderSell()
        {
            var items = GameContext.Player.Items.Where(i => i.EquippedSlot == null).ToList();
            if (items.Count == 0)
            {
                Ui.Label(Ui.Row(_list, "Empty", 80).transform, "Text", "판매할 수 있는 아이템이 없습니다. (장착 중인 장비는 먼저 해제하세요)", 26, TextAnchor.MiddleCenter);
                return;
            }
            foreach (var item in items)
            {
                var row = Row(item.TemplateId, ItemText.NameOf(item.TemplateId, item.EnhanceLevel, item.Quantity),
                    $"개당 {item.SellPrice:N0} 골드", "Sell_" + item.Id);
                ActionButton(row, "Sell1Button", "1개 판매", 0, () => Sell(item, 1));
                if (item.Quantity > 1) ActionButton(row, "SellAllButton", "모두 판매", 1, () => Sell(item, item.Quantity));
            }
        }

        private Transform Row(string templateId, string name, string price, string rowName)
        {
            var row = Ui.Row(_list, rowName, 110);
            var icon = Ui.Node(row.transform, "Icon").gameObject.AddComponent<RawImage>();
            icon.texture = GameResources.Icon(templateId);
            Ui.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(84, 84));
            var nameText = Ui.Label(row.transform, "NameText", name, 30);
            Ui.Place(nameText.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(120, 0), new Vector2(460, 90));
            var priceText = Ui.Label(row.transform, "PriceText", price, 28, TextAnchor.MiddleLeft, UiColors.Gold);
            Ui.Place(priceText.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(590, 0), new Vector2(230, 90));
            return row.transform;
        }

        private static void ActionButton(Transform row, string name, string label, int slot, System.Action onClick)
        {
            var button = Ui.Button(row, name, label, onClick, UiColors.ButtonAccent, 26);
            Ui.Place(Ui.Rect(button), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16 - slot * 170, 0), new Vector2(160, 80));
        }

        private void Buy(ItemTemplate template, int quantity)
        {
            AsyncUi.Run(async () =>
            {
                var inventory = await GameContext.Api.BuyAsync(template.Id, quantity);
                GameContext.Player.Apply(inventory);
                Toast.Show($"{template.Name} {quantity}개를 구매했습니다.");
            });
        }

        private void Sell(ItemDto item, int quantity)
        {
            AsyncUi.Run(async () =>
            {
                var inventory = await GameContext.Api.SellAsync(item.Id, quantity);
                GameContext.Player.Apply(inventory);
                Toast.Show($"{ItemText.NameOf(item.TemplateId, item.EnhanceLevel)} {quantity}개를 판매했습니다.");
            });
        }
    }
}
