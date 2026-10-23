// 클라이언트 Domain(몬스터 AI·봇 의사결정·자동사냥·전투 공식·에러 문구) EditMode 테스트
using System.Linq;
using System.Reflection;
using Ashveil.Domain;
using Ashveil.Shared;
using NUnit.Framework;

namespace Ashveil.Tests
{
    public class MonsterBrainTests
    {
        private static readonly MonsterInfo Imp = MonsterCatalog.Get(MonsterType.Imp);
        private static readonly Vec2 Home = new Vec2(100, 100);

        private static MonsterSenses Near(float distance, bool safe = false) => new MonsterSenses
        {
            Position = Home,
            HasTarget = true,
            TargetPosition = new Vec2(Home.X + distance, Home.Z),
            TargetInSafeZone = safe,
        };

        [Test]
        public void TargetInsideAggroRadius_StartsChase_ThenAttacksInRange()
        {
            var brain = new MonsterBrain(Imp, Home, 1);
            var chase = brain.Tick(0.1f, Near(Imp.AggroRadius - 1));
            Assert.AreEqual(MonsterCommandKind.MoveTo, chase.Kind);
            Assert.AreEqual(MonsterState.Chase, brain.State);

            var attack = brain.Tick(0.1f, Near(Imp.AttackRange - 0.2f));
            Assert.AreEqual(MonsterCommandKind.Attack, attack.Kind);
            Assert.AreEqual(MonsterState.Attack, brain.State);

            var cooling = brain.Tick(0.1f, Near(Imp.AttackRange - 0.2f));
            Assert.AreEqual(MonsterCommandKind.Idle, cooling.Kind, "공격 간격 동안은 다시 때리지 않는다");
            var again = brain.Tick(Imp.AttackInterval, Near(Imp.AttackRange - 0.2f));
            Assert.AreEqual(MonsterCommandKind.Attack, again.Kind);
        }

        [Test]
        public void TargetOutsideAggro_IsIgnoredUnlessItHits()
        {
            var brain = new MonsterBrain(Imp, Home, 2);
            brain.Tick(0.1f, Near(Imp.AggroRadius + 3));
            Assert.AreEqual(MonsterState.Wander, brain.State);

            var hit = Near(Imp.AggroRadius + 3);
            hit.WasHit = true;
            brain.Tick(0.1f, hit);
            Assert.AreEqual(MonsterState.Chase, brain.State);
        }

        [Test]
        public void DraggedBeyondLeash_ReturnsHome_AndIgnoresTargetUntilBack()
        {
            var brain = new MonsterBrain(Imp, Home, 3);
            brain.Tick(0.1f, Near(2));
            var far = new MonsterSenses
            {
                Position = new Vec2(Home.X + Imp.LeashRadius + 1, Home.Z),
                HasTarget = true,
                TargetPosition = new Vec2(Home.X + Imp.LeashRadius + 2, Home.Z),
            };
            var command = brain.Tick(0.1f, far);
            Assert.AreEqual(MonsterState.Return, brain.State);
            Assert.AreEqual(MonsterCommandKind.MoveTo, command.Kind);
            Assert.AreEqual(Home.X, command.Destination.X, 0.01f);

            var midway = far;
            midway.Position = new Vec2(Home.X + 5, Home.Z);
            midway.TargetPosition = new Vec2(Home.X + 6, Home.Z);
            brain.Tick(0.1f, midway);
            Assert.AreEqual(MonsterState.Return, brain.State, "돌아가는 중에는 다시 어그로가 끌리지 않는다");
        }

        [Test]
        public void TargetInSafeZone_IsNotChased()
        {
            var brain = new MonsterBrain(Imp, Home, 4);
            brain.Tick(0.1f, Near(2, safe: true));
            Assert.AreNotEqual(MonsterState.Chase, brain.State);
            Assert.AreNotEqual(MonsterState.Attack, brain.State);
        }

        [Test]
        public void DeadMonster_DoesNothing_UntilRevived()
        {
            var brain = new MonsterBrain(Imp, Home, 5);
            brain.Kill();
            Assert.AreEqual(MonsterCommandKind.Idle, brain.Tick(0.1f, Near(1)).Kind);
            brain.Revive();
            Assert.AreEqual(MonsterState.Wander, brain.State);
        }
    }

    public class BotBrainTests
    {
        private static BotSenses PlayerAt(float distance, AlignmentState playerState = AlignmentState.Innocent) => new BotSenses
        {
            Position = new Vec2(100, 150),
            HpRatio = 1f,
            PlayerVisible = true,
            PlayerPosition = new Vec2(100 + distance, 150),
            PlayerState = playerState,
        };

        [Test]
        public void WhenAttacked_PeacefulFlees_OthersFightBack()
        {
            var senses = PlayerAt(5);
            senses.AttackedByPlayerRecently = true;
            Assert.AreEqual(BotActionKind.Flee, BotBrain.Decide(Temperament.Peaceful, AlignmentState.Innocent, senses));
            Assert.AreEqual(BotActionKind.AttackPlayer, BotBrain.Decide(Temperament.Retaliate, AlignmentState.Innocent, senses));
            Assert.AreEqual(BotActionKind.AttackPlayer, BotBrain.Decide(Temperament.Aggressive, AlignmentState.Innocent, senses));
        }

        [Test]
        public void InnocentPlayer_OnlyAggressiveBotsAttackFirst()
        {
            var senses = PlayerAt(5);
            Assert.AreEqual(BotActionKind.Wander, BotBrain.Decide(Temperament.Peaceful, AlignmentState.Innocent, senses));
            Assert.AreEqual(BotActionKind.Wander, BotBrain.Decide(Temperament.Retaliate, AlignmentState.Innocent, senses));
            Assert.AreEqual(BotActionKind.AttackPlayer, BotBrain.Decide(Temperament.Aggressive, AlignmentState.Innocent, senses));
        }

        [Test]
        public void ChaoticPlayer_IsAttackedByRetaliateBots()
        {
            var senses = PlayerAt(5, AlignmentState.Chaotic);
            Assert.AreEqual(BotActionKind.AttackPlayer, BotBrain.Decide(Temperament.Retaliate, AlignmentState.Innocent, senses));
            Assert.AreEqual(BotActionKind.Flee, BotBrain.Decide(Temperament.Peaceful, AlignmentState.Innocent, senses));
        }

        [Test]
        public void SafeZone_PreventsPk()
        {
            var senses = PlayerAt(5, AlignmentState.Chaotic);
            senses.PlayerInSafeZone = true;
            Assert.AreNotEqual(BotActionKind.AttackPlayer, BotBrain.Decide(Temperament.Aggressive, AlignmentState.Innocent, senses));
        }

        [Test]
        public void FarPlayer_IsIgnored_AndNearbyItemIsPickedUp()
        {
            var senses = PlayerAt(BotBrain.PlayerSightRange + 5);
            senses.GroundItemVisible = true;
            senses.GroundItemPosition = new Vec2(105, 150);
            Assert.AreEqual(BotActionKind.PickupItem, BotBrain.Decide(Temperament.Aggressive, AlignmentState.Innocent, senses));
        }

        [Test]
        public void ChaoticBot_DoesNotVisitTown()
        {
            var senses = PlayerAt(50);
            senses.TownVisitDue = true;
            Assert.AreEqual(BotActionKind.GoToTown, BotBrain.Decide(Temperament.Peaceful, AlignmentState.Innocent, senses));
            Assert.AreNotEqual(BotActionKind.GoToTown, BotBrain.Decide(Temperament.Peaceful, AlignmentState.Chaotic, senses));
        }

        [Test]
        public void SpawnPoint_IsInsideBotZone()
        {
            foreach (var zone in new[] { ZoneId.Beginner, ZoneId.Intermediate, ZoneId.Boss })
            for (long id = 1; id <= 30; id++)
            {
                var p = BotBrain.SpawnPoint(id, zone);
                Assert.AreEqual(zone, ZoneMap.GetZone(p.X, p.Z), $"봇 {id} {zone} {p}");
            }
        }
    }

    public class AutoHuntAndCombatTests
    {
        [Test]
        public void LowHp_DrinksPotion_WhenAvailable()
        {
            var input = new AutoHuntInput { HpRatio = 0.4f, PotionCount = 3, PotionReady = true };
            Assert.AreEqual(AutoHuntAction.UsePotion, AutoHuntPolicy.Decide(input));
            input.PotionCount = 0;
            Assert.AreEqual(AutoHuntAction.FindTarget, AutoHuntPolicy.Decide(input), "마을 안에서도 사냥터 몬스터를 찾아 나간다");
        }

        [Test]
        public void KeepsCurrentTarget()
        {
            Assert.AreEqual(AutoHuntAction.KeepTarget, AutoHuntPolicy.Decide(new AutoHuntInput { HpRatio = 1f, HasLiveTarget = true }));
        }

        [Test]
        public void Damage_HasFloorOfOne_AndScalesWithMultiplier()
        {
            Assert.AreEqual(1, CombatRules.Damage(1, 100, 1f, 0.5));
            Assert.AreEqual(10, CombatRules.Damage(12, 4, 1f, 0.5));
            Assert.AreEqual(20, CombatRules.Damage(12, 4, 2f, 0.5));
            Assert.AreEqual(9, CombatRules.Damage(12, 4, 1f, 0.0));
            Assert.AreEqual(11, CombatRules.Damage(12, 4, 1f, 1.0));
        }

        [Test]
        public void EveryClass_HasTwoSkills()
        {
            foreach (ClassType c in System.Enum.GetValues(typeof(ClassType)))
            {
                var profile = CombatRules.ProfileOf(c);
                Assert.IsNotNull(profile.Skill1);
                Assert.IsNotNull(profile.Skill2);
                Assert.Greater(profile.AttackRange, 0f);
            }
        }

        [Test]
        public void EveryServerErrorCode_HasKoreanMessage()
        {
            var codes = typeof(ErrorCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue());
            foreach (var code in codes) Assert.IsTrue(ErrorMessages.IsKnown(code), "안내 문구 없음: " + code);
        }
    }
}
