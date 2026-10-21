// 자동사냥: 주변 몬스터를 찾아 공격하고 HP가 낮으면 물약을 자동으로 마신다
using Ashveil.Application;
using Ashveil.Domain;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class AutoHuntController : MonoBehaviour
    {
        private const float TickSeconds = 0.3f;
        // 마을 중앙에서 초보 사냥터 몬스터까지 닿는 거리
        private const float WideSearchRadius = 150f;
        private float _timer;
        private bool _warnedNoTarget;

        private void Update()
        {
            var player = WorldServices.Player;
            if (player == null || !player.AutoHunt || player.Actor.IsDead)
            {
                _warnedNoTarget = false;
                return;
            }
            _timer -= Time.deltaTime;
            if (_timer > 0) return;
            _timer = TickSeconds;

            var state = GameContext.Player;
            var action = AutoHuntPolicy.Decide(new AutoHuntInput
            {
                HpRatio = (float)state.Hp / state.MaxHp,
                PotionCount = state.CountOf(Ashveil.Shared.ItemCatalog.PotionHpSmall) + state.CountOf(Ashveil.Shared.ItemCatalog.PotionHpLarge),
                PotionReady = player.PotionCooldown <= 0,
                // 경비병처럼 공격할 수 없는 대상이 선택돼 있으면 계속 붙잡혀 있지 않고 새로 찾는다.
                HasLiveTarget = player.Target != null && player.Target.isActiveAndEnabled && player.CanAttack(player.Target, out _),
            });

            switch (action)
            {
                case AutoHuntAction.UsePotion:
                    player.UsePotion();
                    break;
                case AutoHuntAction.FindTarget:
                    var target = Actor.Nearest(player.Actor.Flat, AutoHuntPolicy.SearchRadius, IsHuntable)
                                 ?? Actor.Nearest(player.Actor.Flat, WideSearchRadius, IsHuntable);
                    if (target != null) player.SetTarget(target);
                    else if (!_warnedNoTarget) Toast.Show("주변에 사냥할 몬스터가 없습니다.");
                    _warnedNoTarget = target == null;
                    break;
            }
        }

        private static bool IsHuntable(Actor actor) =>
            actor.Team == Team.Monster && actor.isActiveAndEnabled && !actor.InSafeZone;
    }
}
