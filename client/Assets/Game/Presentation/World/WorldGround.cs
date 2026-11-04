// 지형 높이 샘플링과 월드 경계 보정, 생성된 프리팹·이펙트·아이콘 로딩
using System.Collections.Generic;
using Ashveil.Domain;
using Ashveil.Shared;
using UnityEngine;

namespace Ashveil.Presentation
{
    public static class WorldGround
    {
        private static Terrain _terrain;

        public static float HeightAt(float x, float z)
        {
            if (_terrain == null) _terrain = Terrain.activeTerrain;
            return _terrain != null ? _terrain.SampleHeight(new Vector3(x, 0, z)) + _terrain.transform.position.y : 0f;
        }

        public static Vector3 Snap(Vector3 p) => new Vector3(p.x, HeightAt(p.x, p.z), p.z);

        public static Vector3 ToWorld(Vec2 p) => Snap(new Vector3(p.X, 0, p.Z));

        public static Vec2 Flat(Vector3 p) => new Vec2(p.x, p.z);

        public static Vector3 ClampToWorld(Vector3 p) =>
            new Vector3(Mathf.Clamp(p.x, 1f, ZoneMap.WorldSize - 1f), p.y, Mathf.Clamp(p.z, 1f, ZoneMap.WorldSize - 1f));

        public static ZoneId ZoneAt(Vector3 p) => ZoneMap.GetZone(p.x, p.z);

        public static bool IsSafe(Vector3 p) => ZoneMap.IsSafeZone(ZoneAt(p));
    }

    /// <summary>ContentGenerator 가 Resources 아래에 만든 프리팹을 이름 규칙으로 찾는다. 없으면 기본 도형으로 대신한다.</summary>
    public static class GameResources
    {
        private static readonly Dictionary<string, Object> Cache = new Dictionary<string, Object>();

        public static GameObject Prefab(string path) => Load<GameObject>("Prefabs/" + path);

        public static string CharacterKey(ClassType classType, Gender gender) => "Characters/" + classType + "_" + gender;

        public static string MonsterKey(MonsterType type) => "Monsters/" + type;

        public static string NpcKey(NpcRole role) => "Npcs/" + role;

        public static Texture2D Icon(string templateId) => Load<Texture2D>("Icons/" + templateId) ?? Load<Texture2D>("Icons/ground_bag");

        public static Material FallbackMaterial(string name) => Load<Material>("Materials/" + name);

        private static T Load<T>(string path) where T : Object
        {
            if (Cache.TryGetValue(path, out var cached)) return cached as T;
            var asset = Resources.Load<T>(path);
            Cache[path] = asset;
            return asset;
        }
    }

    public static class EffectPlayer
    {
        public const string Hit = "Hit";
        public const string LevelUp = "LevelUp";
        public const string Heal = "Heal";
        public const string Skill = "Skill";
        public const string EnhanceSuccess = "EnhanceSuccess";
        public const string EnhanceFail = "EnhanceFail";
        public const string EnhanceDestroy = "EnhanceDestroy";

        public static void Play(string key, Vector3 position, float lifetime = 3f)
        {
            var prefab = GameResources.Prefab("Vfx/" + key);
            if (prefab == null) return;
            var instance = Object.Instantiate(prefab, position, Quaternion.identity);
            instance.name = "Vfx_" + key;
            Object.Destroy(instance, lifetime);
        }
    }
}
