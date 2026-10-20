// SpawnTable 전 지점의 몬스터 생성·리젠·거리 기반 활성화(풀링)와 처치 보고 연결
using System.Collections.Generic;
using Ashveil.Shared;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class MonsterSpawnSystem : MonoBehaviour
    {
        private const float ActivateDistance = 70f;
        private const float DeactivateDistance = 95f;
        private const float CorpseSeconds = 2.5f;
        private const float TickSeconds = 0.5f;

        private sealed class Slot
        {
            public SpawnPoint Spawn;
            public MonsterController Monster;
            public float RespawnAt;
            public float HideAt = float.MaxValue;
        }

        private readonly List<Slot> _slots = new List<Slot>();
        private float _timer;

        public IEnumerable<MonsterController> ActiveMonsters
        {
            get
            {
                foreach (var slot in _slots)
                    if (slot.Monster != null && slot.Monster.isActiveAndEnabled && !slot.Monster.Actor.IsDead) yield return slot.Monster;
            }
        }

        private void Awake()
        {
            foreach (var spawn in SpawnTable.All) _slots.Add(new Slot { Spawn = spawn });
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0) return;
            _timer = TickSeconds;

            var player = WorldServices.Player;
            if (player == null) return;
            var now = Time.time;
            foreach (var slot in _slots)
            {
                var dx = slot.Spawn.X - player.transform.position.x;
                var dz = slot.Spawn.Z - player.transform.position.z;
                var distance = Mathf.Sqrt(dx * dx + dz * dz);

                if (slot.Monster != null && slot.Monster.Actor.IsDead && now >= slot.HideAt)
                {
                    slot.Monster.gameObject.SetActive(false);
                    slot.HideAt = float.MaxValue;
                }

                var alive = slot.Monster != null && !slot.Monster.Actor.IsDead;
                if (!alive && now >= slot.RespawnAt && distance < ActivateDistance && (slot.Monster == null || !slot.Monster.gameObject.activeSelf))
                {
                    if (slot.Monster == null)
                    {
                        slot.Monster = MonsterController.Create(slot.Spawn, transform);
                        slot.Monster.Killed += (monster, killer) => OnKilled(slot, killer);
                    }
                    else slot.Monster.Respawn();
                    continue;
                }

                if (alive)
                {
                    var engaged = slot.Monster.State == Domain.MonsterState.Chase || slot.Monster.State == Domain.MonsterState.Attack;
                    var active = slot.Monster.gameObject.activeSelf;
                    if (active && distance > DeactivateDistance && !engaged) slot.Monster.gameObject.SetActive(false);
                    else if (!active && distance < ActivateDistance) slot.Monster.gameObject.SetActive(true);
                }
            }
        }

        private void OnKilled(Slot slot, Actor killer)
        {
            slot.RespawnAt = Time.time + slot.Spawn.Info.RespawnSeconds;
            slot.HideAt = Time.time + CorpseSeconds;
            if (killer != null && killer.Team == Team.Player)
                WorldServices.Reporter.ReportMonsterKill(slot.Spawn, killer.transform.position);
        }
    }
}
