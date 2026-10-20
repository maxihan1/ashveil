// Boss Room 그래픽 프리팹을 풀어(Unpack) 누락 스크립트를 걷어 내고, 런타임이 Resources 로 찾는 프리팹으로 저장
using System.Linq;
using Ashveil.Domain;
using Ashveil.Presentation;
using Ashveil.Shared;
using UnityEditor;
using UnityEngine;

namespace Ashveil.Editor
{
    public static class PrefabGenerator
    {
        private const string CharGfx = GeneratedPaths.BossRoom + "/Prefabs/CharGFX";
        private const string Controllers = GeneratedPaths.BossRoom + "/Models";
        private const string Vfx = GeneratedPaths.BossRoom + "/VFX/Prefabs";

        public static void CreateAll()
        {
            if (!GeneratedPaths.HasBossRoom)
            {
                Debug.LogWarning("[Generator] Boss Room 아트가 없어 캐릭터 프리팹을 만들지 않습니다(런타임이 캡슐로 대체).");
                return;
            }
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Controllers + "/CharacterSet.fbx").OfType<Avatar>().FirstOrDefault();

            // 플레이어·봇: 직업별 Boss Room 모델 (전사 = Tank)
            foreach (ClassType classType in System.Enum.GetValues(typeof(ClassType)))
            foreach (Gender gender in System.Enum.GetValues(typeof(Gender)))
            {
                var model = BossRoomClass(classType);
                var source = $"{CharGfx}/CharacterGraphics/PlayerGraphics_{model}_{(gender == Gender.Male ? "Boy" : "Girl")}.prefab";
                var controller = $"{Controllers}/CharacterSetController_{model}.overrideController";
                SaveGraphics(source, GameResources.CharacterKey(classType, gender), avatar, controller);
            }

            // 몬스터: 그래픽 프리팹에 애니메이터가 이미 있다.
            SaveGraphics($"{CharGfx}/ImpGraphics.prefab", GameResources.MonsterKey(MonsterType.Imp), null, null);
            SaveGraphics($"{CharGfx}/VandalImpGraphics.prefab", GameResources.MonsterKey(MonsterType.VandalImp), null, null);
            SaveGraphics($"{CharGfx}/BossGraphics.prefab", GameResources.MonsterKey(MonsterType.ImpBoss), null, null);

            // NPC: 플레이어 직업이 아닌 로그(Rogue) 모델과 탱크 모델 재활용
            SaveGraphics($"{CharGfx}/CharacterGraphics/PlayerGraphics_Rogue_Boy.prefab", GameResources.NpcKey(NpcRole.Merchant), avatar,
                $"{Controllers}/CharacterSetController_Rogue.overrideController");
            SaveGraphics($"{CharGfx}/CharacterGraphics/PlayerGraphics_Rogue_Girl.prefab", GameResources.NpcKey(NpcRole.Enhancer), avatar,
                $"{Controllers}/CharacterSetController_Rogue.overrideController");
            SaveGraphics($"{CharGfx}/CharacterGraphics/PlayerGraphics_Tank_Boy.prefab", GameResources.NpcKey(NpcRole.Guard), avatar,
                $"{Controllers}/CharacterSetController_Tank.overrideController");

            CreateGroundItem();

            // 이펙트 (EffectPlayer 키 → Boss Room VFX)
            SaveVfx(EffectPlayer.Hit, "Shared Hero FX/Hitreact Minor.prefab");
            SaveVfx(EffectPlayer.Skill, "Tank/FX_TankSkill2_hit.prefab");
            SaveVfx(EffectPlayer.Heal, "Shared Hero FX/FX_Buff.prefab");
            SaveVfx(EffectPlayer.LevelUp, "Shared Hero FX/FX_Revive.prefab");
            SaveVfx(EffectPlayer.EnhanceSuccess, "Shared Hero FX/FX_Buff.prefab");
            SaveVfx(EffectPlayer.EnhanceFail, "Shared Hero FX/FX_DropObjectPoof.prefab");
            SaveVfx(EffectPlayer.EnhanceDestroy, "Environment/FX_CrystalBreak.prefab");
        }

        private static string BossRoomClass(ClassType classType)
        {
            switch (classType)
            {
                case ClassType.Archer: return "Archer";
                case ClassType.Mage: return "Mage";
                default: return "Tank";
            }
        }

        /// <summary>원본 프리팹 인스턴스를 완전히 풀고 누락 스크립트를 지운 뒤 새 프리팹으로 저장한다.</summary>
        private static GameObject SaveGraphics(string sourcePath, string key, Avatar avatar, string controllerPath)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (source == null)
            {
                Debug.LogWarning("[Generator] 원본 프리팹 없음: " + sourcePath);
                return null;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = key.Substring(key.LastIndexOf('/') + 1);
            StripMissingScripts(instance);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

            if (controllerPath != null)
            {
                var animator = instance.GetComponent<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
                if (animator.runtimeAnimatorController == null) Debug.LogWarning("[Generator] 애니메이터 컨트롤러 없음: " + controllerPath);
            }
            foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
            {
                animator.applyRootMotion = false;
                if (animator.GetComponent<AnimEventReceiver>() == null) animator.gameObject.AddComponent<AnimEventReceiver>();
            }

            var path = $"{GeneratedPaths.Prefabs}/{key}.prefab";
            GeneratedPaths.EnsureFolder(path.Substring(0, path.LastIndexOf('/')));
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static void SaveVfx(string key, string relative)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Vfx}/{relative}");
            if (source == null)
            {
                Debug.LogWarning("[Generator] VFX 없음: " + relative);
                return;
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "Vfx_" + key;
            StripMissingScripts(instance);
            foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.playOnAwake = true;
                main.loop = false;
            }
            var path = $"{GeneratedPaths.Prefabs}/Vfx/{key}.prefab";
            GeneratedPaths.EnsureFolder(GeneratedPaths.Prefabs + "/Vfx");
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
        }

        /// <summary>바닥 아이템: Boss Room 보물상자 모델 + 반짝임 이펙트(없으면 발광 큐브).</summary>
        private static void CreateGroundItem()
        {
            var root = new GameObject("GroundItem");
            var chest = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratedPaths.BossRoom + "/Models/Animated/chest_ani.FBX");
            if (chest != null)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(chest);
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.transform.SetParent(root.transform, false);
                model.transform.localScale = Vector3.one * 0.35f;
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(animator);
            }
            else
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(root.transform, false);
                cube.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(GeneratedPaths.Materials + "/GroundItem.mat");
            }
            var sparkle = AssetDatabase.LoadAssetAtPath<GameObject>($"{Vfx}/Environment/fx_treasure Chest.prefab");
            if (sparkle != null)
            {
                var fx = (GameObject)PrefabUtility.InstantiatePrefab(sparkle);
                PrefabUtility.UnpackPrefabInstance(fx, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                StripMissingScripts(fx);
                fx.transform.SetParent(root.transform, false);
            }
            StripMissingScripts(root);
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.4f, 1.2f, 1.4f);
            box.center = new Vector3(0, 0.5f, 0);
            GeneratedPaths.EnsureFolder(GeneratedPaths.Prefabs + "/World");
            PrefabUtility.SaveAsPrefabAsset(root, GeneratedPaths.Prefabs + "/World/GroundItem.prefab");
            Object.DestroyImmediate(root);
        }

        public static void StripMissingScripts(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }
    }
}
