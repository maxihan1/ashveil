// World 씬 생성: 400×400m 절차적 지형(높이·텍스처), CC0 자연물, 마을, Boss Room 던전 프랍 보스 투기장, 조명·안개
using System.Collections.Generic;
using Ashveil.Domain;
using Ashveil.Presentation;
using Ashveil.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ashveil.Editor
{
    public static class WorldSceneGenerator
    {
        private const int HeightResolution = 257;
        private const int AlphaResolution = 256;
        private const float MaxHeight = 30f;
        private const float Size = ZoneMap.WorldSize;
        private const string Dungeon = GeneratedPaths.BossRoom + "/Prefabs/Dungeon/Dungeon Pieces";

        // 보스 투기장(보스 지역 안쪽). 남쪽 가운데가 입구다.
        private static readonly Rect Arena = new Rect(142, 332, 116, 64);

        private static System.Random _random;
        private static readonly Dictionary<string, (GameObject prefab, float scale)> PropCache = new Dictionary<string, (GameObject, float)>();

        public static void Create()
        {
            _random = new System.Random(20261021);
            PropCache.Clear();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateLighting();
            var terrain = CreateTerrain();
            var props = new GameObject("Props").transform;
            PlaceNature(props);
            BuildTown(props);
            BuildBossArena(props);
            MarkStatic(props.gameObject);

            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            camera.transform.position = new Vector3(ZoneMap.RespawnX, 20, ZoneMap.RespawnZ - 12);
            camera.GetComponent<Camera>().farClipPlane = 140f;
            new GameObject("WorldRoot", typeof(WorldController));

            // 굽기(bake) 없이 실시간 조명만 쓴다. 씬에 저장되도록 에셋으로 만든다.
            var lighting = new LightingSettings { bakedGI = false, realtimeGI = false };
            AssetDatabase.CreateAsset(lighting, GeneratedPaths.Terrain + "/WorldLighting.lighting");
            Lightmapping.lightingSettings = lighting;
            EditorSceneManager.SaveScene(scene, GeneratedPaths.WorldScene);
            Debug.Log($"[Generator] World 씬 생성: 지형 {terrain.terrainData.size}, 프랍 {props.childCount}개 그룹");
        }

        // ───────────────────────── 지형 ─────────────────────────

        public static float HeightAt(float x, float z)
        {
            var h = 3f + 2.2f * Mathf.PerlinNoise(x * 0.012f + 10f, z * 0.012f + 10f) + 0.8f * Mathf.PerlinNoise(x * 0.05f + 3f, z * 0.05f + 7f);
            if (z > ZoneMap.BeginnerMaxZ) h += 1.8f * Mathf.PerlinNoise(x * 0.03f + 50f, z * 0.03f + 50f) * Mathf.Clamp01((z - ZoneMap.BeginnerMaxZ) / 20f);
            var edge = Mathf.Min(Mathf.Min(x, Size - x), Mathf.Min(z, Size - z));
            if (edge < 25f) h += Mathf.Pow((25f - edge) / 25f, 2f) * 14f;
            h = Flatten(h, x, z, new Rect(ZoneMap.Town.MinX, 0, ZoneMap.Town.MaxX - ZoneMap.Town.MinX, ZoneMap.Town.MaxZ), 3f, 14f);
            h = Flatten(h, x, z, Arena, 5f, 18f);
            // 마을 북문 → 보스 지역 입구까지 길을 완만하게 만든다.
            var road = Mathf.Abs(x - 200f);
            if (z > 70f && z < 335f && road < 10f) h = Mathf.Lerp(h, 3.5f + (z - 70f) / 265f * 1.5f, Mathf.Clamp01(1f - road / 10f) * 0.7f);
            return h;
        }

        private static float Flatten(float h, float x, float z, Rect area, float target, float blend)
        {
            var dx = Mathf.Max(Mathf.Max(area.xMin - x, 0f), x - area.xMax);
            var dz = Mathf.Max(Mathf.Max(area.yMin - z, 0f), z - area.yMax);
            var t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dz * dz) / blend);
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(h, target, t);
        }

        private static Terrain CreateTerrain()
        {
            var data = new TerrainData { heightmapResolution = HeightResolution };
            data.size = new Vector3(Size, MaxHeight, Size);
            var heights = new float[HeightResolution, HeightResolution];
            for (var zi = 0; zi < HeightResolution; zi++)
            for (var xi = 0; xi < HeightResolution; xi++)
            {
                var x = xi / (float)(HeightResolution - 1) * Size;
                var z = zi / (float)(HeightResolution - 1) * Size;
                heights[zi, xi] = HeightAt(x, z) / MaxHeight;
            }
            data.SetHeights(0, 0, heights);

            data.alphamapResolution = AlphaResolution;
            data.terrainLayers = new[]
            {
                Layer("Grass", GeneratedPaths.AmbientCg + "/Grass001_Color.jpg", new Color(0.35f, 0.5f, 0.25f)),
                Layer("Dirt", GeneratedPaths.AmbientCg + "/Ground048_Color.jpg", new Color(0.45f, 0.36f, 0.25f)),
                Layer("Stone", GeneratedPaths.AmbientCg + "/PavingStones070_Color.jpg", new Color(0.5f, 0.5f, 0.5f)),
            };
            var alpha = new float[AlphaResolution, AlphaResolution, 3];
            for (var zi = 0; zi < AlphaResolution; zi++)
            for (var xi = 0; xi < AlphaResolution; xi++)
            {
                var x = (xi + 0.5f) / AlphaResolution * Size;
                var z = (zi + 0.5f) / AlphaResolution * Size;
                var (grass, dirt, stone) = Paint(x, z);
                var sum = grass + dirt + stone;
                alpha[zi, xi, 0] = grass / sum;
                alpha[zi, xi, 1] = dirt / sum;
                alpha[zi, xi, 2] = stone / sum;
            }
            data.SetAlphamaps(0, 0, alpha);
            AssetDatabase.CreateAsset(data, GeneratedPaths.Terrain + "/WorldTerrain.asset");

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            var terrain = go.GetComponent<Terrain>();
            terrain.heightmapPixelError = 8f;
            terrain.basemapDistance = 90f;
            terrain.drawInstanced = true;
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                AssetDatabase.CreateAsset(material, GeneratedPaths.Terrain + "/TerrainLit.mat");
                terrain.materialTemplate = material;
            }
            return terrain;
        }

        private static (float grass, float dirt, float stone) Paint(float x, float z)
        {
            var noise = Mathf.PerlinNoise(x * 0.04f + 100f, z * 0.04f + 100f);
            var zone = ZoneMap.GetZone(x, z);
            var nearRoad = Mathf.Abs(x - 200f) < 4.5f && z > 75f && z < 333f;
            if (nearRoad) return (0.1f, 0.9f, 0.05f);
            switch (zone)
            {
                case ZoneId.Town:
                    var plaza = Vector2.Distance(new Vector2(x, z), new Vector2(200f, 40f)) < 26f;
                    return plaza ? (0.05f, 0.25f, 0.8f) : (0.6f, 0.35f, 0.1f);
                case ZoneId.Boss:
                    return Arena.Contains(new Vector2(x, z)) ? (0.02f, 0.3f, 0.75f) : (0.1f, 0.65f, 0.3f);
                case ZoneId.Intermediate:
                    return (0.45f, 0.5f + noise * 0.3f, 0.05f);
                default:
                    return (0.85f, noise > 0.65f ? 0.4f : 0.08f, 0.02f);
            }
        }

        private static TerrainLayer Layer(string name, string texturePath, Color fallback)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                texture = new Texture2D(4, 4);
                var pixels = new Color[16];
                for (var i = 0; i < pixels.Length; i++) pixels[i] = fallback;
                texture.SetPixels(pixels);
                texture.Apply();
                AssetDatabase.CreateAsset(texture, $"{GeneratedPaths.Terrain}/{name}_Fallback.asset");
            }
            var layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(10, 10) };
            AssetDatabase.CreateAsset(layer, $"{GeneratedPaths.Terrain}/{name}.terrainlayer");
            return layer;
        }

        // ───────────────────────── 자연물 ─────────────────────────

        private static void PlaceNature(Transform props)
        {
            var nature = new GameObject("Nature").transform;
            nature.SetParent(props, false);
            string[] fieldTrees = { "tree_default", "tree_oak", "tree_detailed", "tree_fat", "tree_pineRoundA", "tree_cone", "tree_default_fall", "tree_oak_fall" };
            string[] forestTrees = { "tree_default_dark", "tree_oak_dark", "tree_pineTallA", "tree_pineTallA_detailed", "tree_cone_dark" };
            string[] rocks = { "rock_largeA", "rock_smallA", "rock_tallA", "stone_largeA", "stone_smallA", "stone_tallA" };
            string[] plants = { "plant_bush", "plant_bushLarge", "plant_bushSmall", "grass", "grass_large", "flower_redA" };
            string[] forestFloor = { "mushroom_red", "mushroom_tanGroup", "plant_bushSmall", "grass" };

            for (var i = 0; i < 2600; i++)
            {
                var x = 4f + (float)_random.NextDouble() * (Size - 8f);
                var z = 4f + (float)_random.NextDouble() * (Size - 8f);
                if (!FreeForProp(x, z)) continue;
                var zone = ZoneMap.GetZone(x, z);
                var roll = _random.NextDouble();
                switch (zone)
                {
                    case ZoneId.Beginner:
                        if (roll < 0.18) Prop(nature, Pick(fieldTrees), x, z, Range(6f, 9f));
                        else if (roll < 0.26) Prop(nature, Pick(rocks), x, z, Range(1f, 2.5f));
                        else if (roll < 0.42) Prop(nature, Pick(plants), x, z, Range(0.6f, 1.4f));
                        break;
                    case ZoneId.Intermediate:
                        if (roll < 0.42) Prop(nature, Pick(forestTrees), x, z, Range(7f, 11f));
                        else if (roll < 0.52) Prop(nature, Pick(rocks), x, z, Range(1.2f, 3.5f));
                        else if (roll < 0.62) Prop(nature, Pick(forestFloor), x, z, Range(0.5f, 1.2f));
                        break;
                    case ZoneId.Boss:
                        if (roll < 0.25 && !Arena.Contains(new Vector2(x, z))) Prop(nature, Pick(rocks), x, z, Range(1.5f, 4f));
                        break;
                    case ZoneId.Town:
                        var nearEdge = x < 160f || x > 240f || z < 10f;
                        if (nearEdge && roll < 0.15) Prop(nature, Pick(fieldTrees), x, z, Range(6f, 8f));
                        break;
                }
            }
        }

        /// <summary>스폰 지점·길·NPC·투기장 입구 근처에는 자연물을 두지 않는다.</summary>
        private static bool FreeForProp(float x, float z)
        {
            if (Mathf.Abs(x - 200f) < 7f && z > 70f && z < 335f) return false;
            foreach (var spawn in SpawnTable.All)
                if (Vector2.Distance(new Vector2(x, z), new Vector2(spawn.X, spawn.Z)) < 7f) return false;
            foreach (var npc in WorldLayout.Npcs)
                if (Vector2.Distance(new Vector2(x, z), new Vector2(npc.Position.X, npc.Position.Z)) < 8f) return false;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(ZoneMap.RespawnX, ZoneMap.RespawnZ)) < 25f) return false;
            return true;
        }

        // ───────────────────────── 마을 ─────────────────────────

        private static void BuildTown(Transform props)
        {
            var town = new GameObject("Town").transform;
            town.SetParent(props, false);
            var t = ZoneMap.Town;

            // 울타리: 북쪽(정문 190~210 비움), 서·동쪽(z 30~50 비움)
            FenceLine(town, new Vector2(t.MinX, t.MaxZ), new Vector2(190f, t.MaxZ));
            FenceLine(town, new Vector2(210f, t.MaxZ), new Vector2(t.MaxX, t.MaxZ));
            FenceLine(town, new Vector2(t.MinX, 2f), new Vector2(t.MinX, 30f));
            FenceLine(town, new Vector2(t.MinX, 50f), new Vector2(t.MinX, t.MaxZ));
            FenceLine(town, new Vector2(t.MaxX, 2f), new Vector2(t.MaxX, 30f));
            FenceLine(town, new Vector2(t.MaxX, 50f), new Vector2(t.MaxX, t.MaxZ));

            foreach (var npc in WorldLayout.Npcs)
            {
                if (npc.Role == NpcRole.Guard) continue;
                Prop(town, "tent_detailedOpen", npc.Position.X, npc.Position.Z + 6f, 3.2f, 180f);
                Torch(town, npc.Position.X + 4f, npc.Position.Z + 2f);
            }
            Prop(town, "campfire_logs", 200f, 40f, 0.8f);
            Prop(town, "statue_obelisk", 200f, 14f, 6f);
            Prop(town, "statue_column", 176f, 30f, 4f);
            Prop(town, "statue_column", 224f, 30f, 4f);
            Prop(town, "log_stack", 172f, 60f, 1.2f, 30f);
            Prop(town, "log_stack", 230f, 62f, 1.2f, -20f);
            Prop(town, "pot_large", 182f, 50f, 1f);
            Prop(town, "pot_large", 218f, 50f, 1f);
            Prop(town, "sign", 207f, 84f, 1.8f, 180f);
            Prop(town, "tent_smallClosed", 162f, 18f, 2.6f, 45f);
            Prop(town, "tent_smallClosed", 238f, 18f, 2.6f, -45f);
            Torch(town, 188f, 80f);
            Torch(town, 212f, 80f);
            for (var x = 160f; x <= 240f; x += 10f) Prop(town, "path_stone", x, 40f, 0.3f);
        }

        private static void FenceLine(Transform parent, Vector2 from, Vector2 to)
        {
            var (prefab, unit) = Load("fence_simple", 1.3f);
            if (prefab == null) return;
            var length = Vector2.Distance(from, to);
            var piece = Mathf.Max(0.5f, MeasureLength(prefab) * unit);
            var dir = (to - from).normalized;
            var yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg - 90f;
            for (var d = piece * 0.5f; d < length; d += piece)
            {
                var p = from + dir * d;
                Place(parent, prefab, unit, p.x, p.y, yaw);
            }
        }

        private static void Torch(Transform parent, float x, float z)
        {
            var torch = AssetDatabase.LoadAssetAtPath<GameObject>(Dungeon + "/env_torch.prefab");
            if (torch != null) PlaceBossRoom(parent, torch, x, z, 0f);
        }

        // ───────────────────────── 보스 투기장 ─────────────────────────

        private static void BuildBossArena(Transform props)
        {
            var arena = new GameObject("BossArena").transform;
            arena.SetParent(props, false);
            if (!GeneratedPaths.HasBossRoom)
            {
                for (var x = Arena.xMin; x <= Arena.xMax; x += 12f)
                {
                    Prop(arena, "rock_tallA", x, Arena.yMax, 5f);
                    Prop(arena, "rock_tallA", x, Arena.yMin, 5f);
                }
                return;
            }

            // 바닥 타일
            string[] floors = { "env_floor1", "env_floor2", "env_floor3", "env_floor4" };
            var floorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dungeon}/{floors[0]}.prefab");
            var tile = floorPrefab != null ? Footprint(floorPrefab) : Vector2.zero;
            // 타일이 너무 작으면(측정 실패 등) 수천 개를 깔게 되므로 건너뛴다.
            if (tile.x >= 2f && tile.y >= 2f && Arena.width * Arena.height / (tile.x * tile.y) < 1500f)
            {
                for (var x = Arena.xMin + tile.x * 0.5f; x < Arena.xMax; x += tile.x)
                for (var z = Arena.yMin + tile.y * 0.5f; z < Arena.yMax; z += tile.y)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dungeon}/{floors[_random.Next(floors.Length)]}.prefab") ?? floorPrefab;
                    PlaceBossRoom(arena, prefab, x, z, 90f * _random.Next(4), 0.02f);
                }
            }

            // 벽: 북·서·동쪽과 남쪽(입구 190~210 비움)
            var wall = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dungeon}/env_wall1_base_straight_1.prefab");
            if (wall != null)
            {
                var length = Mathf.Max(1f, MeasureLength(wall));
                WallLine(arena, wall, length, new Vector2(Arena.xMin, Arena.yMax), new Vector2(Arena.xMax, Arena.yMax));
                WallLine(arena, wall, length, new Vector2(Arena.xMin, Arena.yMin), new Vector2(Arena.xMin, Arena.yMax));
                WallLine(arena, wall, length, new Vector2(Arena.xMax, Arena.yMin), new Vector2(Arena.xMax, Arena.yMax));
                WallLine(arena, wall, length, new Vector2(Arena.xMin, Arena.yMin), new Vector2(190f, Arena.yMin));
                WallLine(arena, wall, length, new Vector2(210f, Arena.yMin), new Vector2(Arena.xMax, Arena.yMin));
            }

            var pillar = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dungeon}/pillar/env_pillar.prefab");
            var torch = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dungeon}/env_torch2.prefab");
            for (var x = Arena.xMin + 10f; x < Arena.xMax; x += 24f)
            {
                if (pillar != null)
                {
                    PlaceBossRoom(arena, pillar, x, Arena.yMax - 4f, 0f);
                    if (Mathf.Abs(x - 200f) > 14f) PlaceBossRoom(arena, pillar, x, Arena.yMin + 4f, 0f);
                }
                if (torch != null) PlaceBossRoom(arena, torch, x + 12f, Arena.yMax - 2f, 180f);
            }
            if (torch != null)
            {
                PlaceBossRoom(arena, torch, 186f, Arena.yMin - 1f, 0f);
                PlaceBossRoom(arena, torch, 214f, Arena.yMin - 1f, 0f);
            }

            var lava = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dungeon}/env_lava.prefab");
            if (lava != null)
            {
                PlaceBossRoom(arena, lava, Arena.xMin + 12f, Arena.yMax - 12f, 0f, 0.05f);
                PlaceBossRoom(arena, lava, Arena.xMax - 12f, Arena.yMax - 12f, 0f, 0.05f);
            }
            var banner = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dungeon}/env_banner.prefab");
            if (banner != null)
            {
                PlaceBossRoom(arena, banner, 188f, Arena.yMax - 1.5f, 180f);
                PlaceBossRoom(arena, banner, 212f, Arena.yMax - 1.5f, 180f);
            }
            foreach (var name in new[] { "env_rock", "env_pebbles", "env_roots", "env_spiderweb_1" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dungeon}/{name}.prefab");
                if (prefab == null) continue;
                for (var i = 0; i < 6; i++)
                    PlaceBossRoom(arena, prefab, Range(Arena.xMin + 4f, Arena.xMax - 4f), Range(Arena.yMin + 4f, Arena.yMax - 4f), Range(0f, 360f));
            }

            // 용암 빛
            foreach (var p in new[] { new Vector2(Arena.xMin + 12f, Arena.yMax - 12f), new Vector2(Arena.xMax - 12f, Arena.yMax - 12f), new Vector2(200f, 372f) })
            {
                var light = new GameObject("LavaLight", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.45f, 0.15f);
                light.range = 22f;
                light.intensity = 2.5f;
                light.transform.SetParent(arena, false);
                light.transform.position = new Vector3(p.x, HeightAt(p.x, p.y) + 4f, p.y);
            }
        }

        private static void WallLine(Transform parent, GameObject wall, float length, Vector2 from, Vector2 to)
        {
            var total = Vector2.Distance(from, to);
            var dir = (to - from).normalized;
            var yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg - 90f;
            for (var d = length * 0.5f; d < total; d += length)
            {
                var p = from + dir * d;
                PlaceBossRoom(parent, wall, p.x, p.y, yaw);
            }
        }

        // ───────────────────────── 조명 ─────────────────────────

        private static void CreateLighting()
        {
            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.62f, 0.75f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.42f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.22f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 135f;
            RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.75f);
            var skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (skybox != null) RenderSettings.skybox = skybox;
        }

        // ───────────────────────── 배치 도우미 ─────────────────────────

        /// <summary>Kenney 모델을 목표 높이(m)에 맞춰 배치한다(FBX 단위 차이를 측정값으로 보정).</summary>
        private static void Prop(Transform parent, string model, float x, float z, float targetHeight, float? yaw = null)
        {
            var (prefab, unit) = Load(model, 1f);
            if (prefab == null) return;
            Place(parent, prefab, unit * targetHeight, x, z, yaw ?? Range(0f, 360f));
        }

        /// <summary>(프리팹, 높이 1m 가 되는 배율)</summary>
        private static (GameObject prefab, float scale) Load(string model, float targetHeight)
        {
            if (!PropCache.TryGetValue(model, out var cached))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{GeneratedPaths.Kenney}/{model}.fbx");
                var height = prefab != null ? Measure(prefab).size.y : 0f;
                cached = (prefab, height > 0.01f ? 1f / height : 1f);
                PropCache[model] = cached;
            }
            return (cached.prefab, cached.scale * targetHeight);
        }

        private static void Place(Transform parent, GameObject prefab, float scale, float x, float z, float yaw)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = new Vector3(x, HeightAt(x, z), z);
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
        }

        private static void PlaceBossRoom(Transform parent, GameObject prefab, float x, float z, float yaw, float lift = 0f)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            PrefabGenerator.StripMissingScripts(instance);
            instance.transform.position = new Vector3(x, HeightAt(x, z) + lift, z);
            instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static Bounds Measure(GameObject prefab)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = Vector3.zero;
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (i == 0) bounds = renderers[i].bounds;
                else bounds.Encapsulate(renderers[i].bounds);
            }
            Object.DestroyImmediate(instance);
            return bounds;
        }

        private static float MeasureLength(GameObject prefab)
        {
            var size = Measure(prefab).size;
            return Mathf.Max(size.x, size.z);
        }

        private static Vector2 Footprint(GameObject prefab)
        {
            var size = Measure(prefab).size;
            return new Vector2(size.x, size.z);
        }

        private static void MarkStatic(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.GetComponent<Light>() != null || t.GetComponentInChildren<ParticleSystem>(true) != null) continue;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }
        }

        private static string Pick(string[] options) => options[_random.Next(options.Length)];

        private static float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
    }
}
