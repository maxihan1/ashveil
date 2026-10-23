// 마을 NPC: 상인·강화사(탭하면 상점·강화 창)와 악명 캐릭터를 공격하는 경비병
using Ashveil.Domain;
using Ashveil.Shared;
using UnityEngine;

namespace Ashveil.Presentation
{
    public sealed class NpcController : MonoBehaviour
    {
        public NpcPlacement Placement { get; private set; }
        public Actor Actor { get; private set; }
        public bool IsInteractable => Placement.Role != NpcRole.Guard;

        private ActorMotor _motor;
        private float _attackTimer;
        private Vector3 _post;

        public static NpcController Create(NpcPlacement placement, Transform parent)
        {
            var root = new GameObject("Npc_" + placement.Id);
            root.transform.SetParent(parent, false);
            var visual = CharacterVisual.Attach(root, GameResources.Prefab(GameResources.NpcKey(placement.Role)), new Color(0.4f, 0.8f, 0.4f));
            var actor = root.AddComponent<Actor>();
            actor.Visual = visual;
            actor.NameplateHeight = visual.Height + 0.3f;
            actor.Init(placement.Role == NpcRole.Guard ? Team.Guard : Team.Npc, placement.Name, 99, 99999, 0, 999);
            actor.Invulnerable = true;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.center = Vector3.up;
            collider.height = 2f;
            collider.radius = 0.7f;
            var motor = root.AddComponent<ActorMotor>();
            motor.Speed = 6.5f;

            var npc = root.AddComponent<NpcController>();
            npc.Placement = placement;
            npc.Actor = actor;
            npc._motor = motor;
            motor.Teleport(new Vector3(placement.Position.X, 0, placement.Position.Z));
            root.transform.rotation = Quaternion.Euler(0, placement.Yaw, 0);
            npc._post = root.transform.position;
            return npc;
        }

        public void Interact()
        {
            switch (Placement.Role)
            {
                case NpcRole.Merchant:
                    WorldServices.Hud.OpenShop();
                    break;
                case NpcRole.Enhancer:
                    WorldServices.Hud.OpenEnhance();
                    break;
            }
        }

        private void Update()
        {
            if (Placement.Role != NpcRole.Guard) return;
            _attackTimer -= Time.deltaTime;
            var player = WorldServices.Player;
            var chaotic = player != null && !player.Actor.IsDead && player.Actor.Alignment == AlignmentState.Chaotic
                          && Time.time >= player.ProtectedUntil;
            var nearTown = player != null && NearTown(player.transform.position);
            var inRange = player != null && Vector3.Distance(_post, player.transform.position) < WorldLayout.GuardAggroRange * 1.5f;

            if (chaotic && nearTown && inRange)
            {
                if (_motor.MoveTowards(player.transform.position, 2.2f))
                {
                    _motor.Face(player.transform.position);
                    if (_attackTimer <= 0)
                    {
                        _attackTimer = 1.2f;
                        Actor.Visual.Attack();
                        // 경비병 일격은 최대 HP의 25%. 악명 캐릭터는 마을에 오래 머물 수 없다.
                        player.Actor.TakeDamage(Actor, Mathf.Max(1, player.Actor.MaxHp / 4));
                    }
                }
                return;
            }

            if (_motor.MoveTowards(_post, 0.5f)) transform.rotation = Quaternion.Euler(0, Placement.Yaw, 0);
        }

        private static bool NearTown(Vector3 p)
        {
            var town = ZoneMap.Town;
            const float margin = 15f;
            return p.x > town.MinX - margin && p.x < town.MaxX + margin && p.z < town.MaxZ + margin;
        }
    }
}
