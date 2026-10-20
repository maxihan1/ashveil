// 자동사냥: 주변 몬스터를 찾아 공격하고 HP가 낮으면 물약을 자동으로 마신다
using Ashveil.Application;
using Ashveil.Domain;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class AutoHuntController : MonoBehaviour
    {
        private const float TickSeconds = 0.3f;
        private const float WideSearchRadius = 60f;
        private float _timer;

        private void Update()
        {
            var player = WorldServices.Player;
            if (player == null || !player.AutoHunt || player.Actor.IsDead) return;
            _timer -= Time.deltaTime;
            if (_timer > 0) return;
            _timer = TickSeconds;

            var state = GameContext.Player;
            var action = AutoHuntPolicy.Decide(new AutoHuntInput
            {
                HpRatio = (float)state.Hp / state.MaxHp,
                PotionCount = state.CountOf(Ashveil.Shared.ItemCatalog.PotionHpSmall) + state.CountOf(Ashveil.Shared.ItemCatalog.PotionHpLarge),
                PotionReady = player.PotionCooldown <= 0,
                HasLiveTarget = player.Target != null && !player.Target.IsDead && player.Target.isActiveAndEnabled,
                InSafeZone = player.Actor.InSafeZone,
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
                    break;
            }
        }

        private static bool IsHuntable(Actor actor) =>
            actor.Team == Team.Monster && actor.isActiveAndEnabled && !actor.InSafeZone;
    }
}
