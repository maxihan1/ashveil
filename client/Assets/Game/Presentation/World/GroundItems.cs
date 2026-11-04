// 바닥에 떨어진 아이템 표시·동기화와 플레이어·봇 줍기 요청
using System;
using System.Collections.Generic;
using System.Linq;
using Ashveil.Application;
using Ashveil.Domain;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class GroundItemView : MonoBehaviour
    {
        public GroundItemDto Data { get; private set; }
        public bool Claimed { get; set; }

        public static GroundItemView Create(GroundItemDto data, Transform parent)
        {
            var prefab = GameResources.Prefab("World/GroundItem");
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, parent);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(parent, false);
                go.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
                var material = GameResources.FallbackMaterial("GroundItem");
                if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            }
            go.name = "GroundItem_" + data.Id;
            if (go.GetComponent<Collider>() == null)
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = Vector3.one * 1.5f;
            }
            foreach (var c in go.GetComponentsInChildren<Collider>()) c.isTrigger = true;
            go.transform.position = WorldGround.Snap(new Vector3(data.X, 0, data.Z)) + Vector3.up * 0.2f;
            var view = go.AddComponent<GroundItemView>();
            view.Data = data;
            // 이름표는 비전투 개체로 Actor 목록에 올려 NameplateLayer 가 그리게 한다.
            var label = go.AddComponent<Actor>();
            label.Init(Team.Npc, ItemText.NameOf(data.TemplateId, data.EnhanceLevel, data.Quantity), 0, 1, 0, 0);
            label.Invulnerable = true;
            label.NameplateHeight = 1.2f;
            return view;
        }
    }

    public sealed class GroundItemManager : MonoBehaviour
    {
        private const float PollSeconds = 4f;
        private readonly Dictionary<long, GroundItemView> _views = new Dictionary<long, GroundItemView>();
        private float _timer;
        private bool _polling;
        private ZoneId _zone;

        public IEnumerable<GroundItemView> Views => _views.Values;

        public void RefreshSoon() => _timer = 0;

        private void Update()
        {
            var player = WorldServices.Player;
            if (player == null) return;
            var zone = WorldGround.ZoneAt(player.transform.position);
            if (zone != _zone)
            {
                _zone = zone;
                _timer = 0;
            }
            _timer -= Time.deltaTime;
            if (_timer > 0 || _polling) return;
            _timer = PollSeconds;
            _polling = true;
            AsyncUi.Run(async () =>
            {
                try
                {
                    var list = await GameContext.Api.GetGroundItemsAsync(_zone);
                    if (this != null) Sync(list);
                }
                finally
                {
                    _polling = false;
                }
            }, e => Debug.LogWarning("[Ground] " + e.Code + " " + e.Message));
        }

        private void Sync(List<GroundItemDto> list)
        {
            var ids = new HashSet<long>(list.Select(i => i.Id));
            foreach (var id in _views.Keys.Where(id => !ids.Contains(id)).ToList()) Remove(id);
            foreach (var dto in list)
                if (!_views.ContainsKey(dto.Id)) _views[dto.Id] = GroundItemView.Create(dto, transform);
        }

        private void Remove(long id)
        {
            if (!_views.TryGetValue(id, out var view)) return;
            if (view != null) Destroy(view.gameObject);
            _views.Remove(id);
        }

        public GroundItemView NearestFor(ZoneId zone, Vec2 from, float radius)
        {
            GroundItemView best = null;
            var bestDistance = radius;
            foreach (var view in _views.Values)
            {
                if (view == null || view.Claimed || view.Data.Zone != zone) continue;
                var d = Vec2.Distance(from, WorldGround.Flat(view.transform.position));
                if (d <= bestDistance)
                {
                    best = view;
                    bestDistance = d;
                }
            }
            return best;
        }

        public void PlayerPickup(GroundItemView view)
        {
            if (view == null || view.Claimed) return;
            view.Claimed = true;
            var id = view.Data.Id;
            var position = WorldServices.Player.transform.position;
            AsyncUi.Run(async () =>
            {
                var result = await GameContext.Api.PickupAsync(id, null, position.x, position.z);
                GameContext.Player.Apply(result.Inventory);
                Toast.Show(ItemText.NameOf(result.Item.TemplateId, result.Item.EnhanceLevel, result.Item.Quantity) + "을(를) 주웠습니다.");
                Remove(id);
            }, e =>
            {
                Toast.Show(e.Message);
                if (e.Code == ErrorCodes.NotFound || e.Code == ErrorCodes.ItemExpired) Remove(id);
                else if (view != null) view.Claimed = false;
            });
        }

        public void BotPickup(BotController bot, GroundItemView view, Action done)
        {
            if (view == null || view.Claimed)
            {
                done();
                return;
            }
            view.Claimed = true;
            var id = view.Data.Id;
            var position = bot.transform.position;
            AsyncUi.Run(async () =>
            {
                try
                {
                    await GameContext.Api.PickupAsync(id, bot.Data.Id, position.x, position.z);
                    Debug.Log($"[Ground] 봇 {bot.Data.Name} 이(가) 아이템 {id} 을(를) 주웠습니다.");
                    Remove(id);
                    WorldServices.Bots.RefreshSoon();
                }
                finally
                {
                    done();
                }
            }, e =>
            {
                if (e.Code == ErrorCodes.NotFound || e.Code == ErrorCodes.ItemExpired) Remove(id);
                else if (view != null) view.Claimed = false;
            });
        }
    }
}
