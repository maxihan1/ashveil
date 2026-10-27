// 전투 결과를 서버에 보고하고(몬스터·봇 처치, PK 선공, 사망) 응답으로 화면 상태를 갱신한다
using System.Collections.Generic;
using Ashveil.Application;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class CombatReporter : MonoBehaviour
    {
        private const float PkReportThrottleSeconds = 10f;
        private readonly Dictionary<string, float> _pkReportedAt = new Dictionary<string, float>();

        private static PlayerState Player => GameContext.Player;

        public void ReportMonsterKill(SpawnPoint spawn, Vector3 killerPosition)
        {
            AsyncUi.Run(async () =>
            {
                var result = await GameContext.Api.ReportMonsterKillAsync(spawn.Monster, spawn.Id, killerPosition.x, killerPosition.z);
                Player.Apply(result.Character, fillVitals: result.LeveledUp);
                var player = WorldServices.Player;
                if (player != null)
                {
                    NameplateLayer.PopupAt(player.Actor.HeadPosition + Vector3.up * 0.6f, $"+{result.GoldGained} 골드", UiColors.Gold);
                    NameplateLayer.PopupAt(player.Actor.HeadPosition, $"+{result.ExpGained} 경험치", UiColors.Exp);
                }
                if (result.LeveledUp)
                {
                    Toast.Show($"레벨 업! Lv.{result.Character.Level}");
                    if (player != null) EffectPlayer.Play(EffectPlayer.LevelUp, player.transform.position);
                }
            }, e => Debug.LogWarning($"[Combat] monster-kill {spawn.Id} 거부: {e.Code}"));
        }

        public void ReportBotKill(BotController bot, Vector3 killerPosition)
        {
            AsyncUi.Run(async () =>
            {
                var result = await GameContext.Api.ReportBotKillAsync(bot.Data.Id, killerPosition.x, killerPosition.z);
                Player.Apply(result.Character);
                if (bot != null) bot.Apply(result.Bot);
                if (result.AlignmentDelta < 0) Toast.Show($"선량한 캐릭터를 처치해 성향치가 {result.AlignmentDelta} 되었습니다.");
                if (result.Dropped != null)
                {
                    var name = ItemText.NameOf(result.Dropped.TemplateId, result.Dropped.EnhanceLevel, result.Dropped.Quantity);
                    if (result.DroppedToGround)
                    {
                        Toast.Show($"인벤토리가 가득 차서 {name}이(가) 바닥에 떨어졌습니다.");
                        WorldServices.Ground.RefreshSoon();
                    }
                    else
                    {
                        Toast.Show($"{bot?.Data.Name}에게서 {name}을(를) 얻었습니다.");
                        Player.Apply(await GameContext.Api.GetInventoryAsync());
                    }
                }
                WorldServices.Bots.RefreshSoon();
            }, e =>
            {
                Toast.Show(e.Message);
                WorldServices.Bots.RefreshSoon();
            });
        }

        /// <summary>플레이어가 봇을 때렸을 때(선공 판정). 같은 대상은 일정 시간에 한 번만 보고한다.</summary>
        public void ReportPlayerAttackedBot(BotController bot)
        {
            var me = GameContext.Session.CharacterId;
            if (!ShouldReport("p" + bot.Data.Id)) return;
            var position = WorldServices.Player.transform.position;
            AsyncUi.Run(async () =>
            {
                var result = await GameContext.Api.ReportPkAttackAsync(EntityKind.Character, me, EntityKind.Bot, bot.Data.Id, position.x, position.z);
                Player.Apply(result.Attacker);
                if (bot != null) bot.ApplyAlignment(result.Target);
                if (result.AttackerBecamePurple) Toast.Show("선량한 캐릭터를 먼저 공격해 보라색 이름이 되었습니다.");
            }, e => Debug.LogWarning("[Combat] pk-attack 거부: " + e.Code));
        }

        /// <summary>봇이 플레이어를 때렸을 때(봇의 선공 판정).</summary>
        public void ReportBotAttackedPlayer(BotController bot)
        {
            var me = GameContext.Session.CharacterId;
            if (!ShouldReport("b" + bot.Data.Id)) return;
            var position = WorldServices.Player.transform.position;
            AsyncUi.Run(async () =>
            {
                var result = await GameContext.Api.ReportPkAttackAsync(EntityKind.Bot, bot.Data.Id, EntityKind.Character, me, position.x, position.z);
                if (bot != null) bot.ApplyAlignment(result.Attacker);
                Player.Apply(result.Target);
            }, e => Debug.LogWarning("[Combat] pk-attack(bot) 거부: " + e.Code));
        }

        public void ReportPlayerDeath(Actor killer)
        {
            var kind = EntityKind.Monster;
            long? killerId = null;
            if (killer != null && killer.Team == Team.Bot && killer.TryGetComponent<BotController>(out var bot))
            {
                kind = EntityKind.Bot;
                killerId = bot.Data.Id;
            }
            else if (killer != null && killer.Team == Team.Guard) kind = EntityKind.Guard;

            var position = WorldServices.Player.transform.position;
            var killerName = killer != null ? killer.DisplayName : "알 수 없음";
            AsyncUi.Run(async () =>
            {
                var result = await GameContext.Api.ReportPlayerDeathAsync(kind, killerId, position.x, position.z);
                Player.Apply(result.Character);
                WorldServices.Hud.ShowDeath(killerName, result.ExpLost, result.Dropped);
                if (result.Dropped != null)
                {
                    Player.Apply(await GameContext.Api.GetInventoryAsync());
                    WorldServices.Ground.RefreshSoon();
                }
            }, e =>
            {
                Toast.Show(e.Message);
                WorldServices.Hud.ShowDeath(killerName, 0, null);
            });
        }

        private bool ShouldReport(string key)
        {
            if (_pkReportedAt.TryGetValue(key, out var at) && Time.time - at < PkReportThrottleSeconds) return false;
            _pkReportedAt[key] = Time.time;
            return true;
        }
    }
}
