// 마을 NPC·경비병·장식물 배치 좌표 (씬 생성기와 런타임이 함께 쓴다)
using System.Collections.Generic;
using Ashveil.Shared;

namespace Ashveil.Domain
{
    public enum NpcRole { Merchant, Enhancer, Guard }

    public sealed class NpcPlacement
    {
        public string Id { get; }
        public NpcRole Role { get; }
        public string Name { get; }
        public Vec2 Position { get; }
        /// <summary>바라보는 방향(Y축 회전, 도).</summary>
        public float Yaw { get; }

        public NpcPlacement(string id, NpcRole role, string name, Vec2 position, float yaw)
        {
            Id = id; Role = role; Name = name; Position = position; Yaw = yaw;
        }
    }

    public static class WorldLayout
    {
        public const float InteractRange = 4f;
        public const float GuardAggroRange = 18f;

        public static readonly IReadOnlyList<NpcPlacement> Npcs = new List<NpcPlacement>
        {
            new NpcPlacement("npc_merchant", NpcRole.Merchant, "상인 브릭", new Vec2(188f, 46f), 150f),
            new NpcPlacement("npc_enhancer", NpcRole.Enhancer, "대장장이 오른", new Vec2(212f, 46f), 210f),
            new NpcPlacement("npc_guard_w", NpcRole.Guard, "경비병", new Vec2(165f, 76f), 0f),
            new NpcPlacement("npc_guard_c", NpcRole.Guard, "경비병", new Vec2(200f, 78f), 0f),
            new NpcPlacement("npc_guard_e", NpcRole.Guard, "경비병", new Vec2(235f, 76f), 0f),
        };

        /// <summary>마을 광장 중심.</summary>
        public static readonly Vec2 TownPlaza = new Vec2(200f, 40f);

        /// <summary>보스 지역 입구(사냥터 쪽에서 들어오는 길목).</summary>
        public static readonly Vec2 BossGate = new Vec2(200f, 322f);

        public static Vec2 RespawnPoint => new Vec2(ZoneMap.RespawnX, ZoneMap.RespawnZ);
    }
}
