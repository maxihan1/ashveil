// 에디터 없이 CI에서 실행하는 콘텐츠 생성 진입점: 프로젝트 설정, URP, 머티리얼, 프리팹, Boot·World 씬
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Ashveil.Editor
{
    public static class GeneratedPaths
    {
        public const string Root = "Assets/Game/Generated";
        public const string Resources = Root + "/Resources";
        public const string Prefabs = Resources + "/Prefabs";
        public const string Materials = Resources + "/Materials";
        public const string Scenes = Root + "/Scenes";
        public const string Settings = Root + "/Settings";
        public const string Terrain = Root + "/Terrain";
        public const string BootScene = Scenes + "/Boot.unity";
        public const string WorldScene = Scenes + "/World.unity";

        public const string BossRoom = "Assets/ThirdParty/BossRoom";
        public const string Kenney = "Assets/ThirdParty/CC0/KenneyNatureKit";
        public const string AmbientCg = "Assets/ThirdParty/CC0/AmbientCG";
        public const string Icons = "Assets/Game/Resources/Icons";

        public static bool HasBossRoom => AssetDatabase.IsValidFolder(BossRoom + "/Models");

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    public static class ContentGenerator
    {
        [MenuItem("Ashveil/Generate All Content")]
        public static void GenerateAll()
        {
            var started = DateTime.Now;
            Debug.Log("[Generator] 시작. Boss Room 아트: " + (GeneratedPaths.HasBossRoom ? "있음" : "없음(대체 도형 사용)"));
            foreach (var folder in new[] { GeneratedPaths.Prefabs, GeneratedPaths.Materials, GeneratedPaths.Scenes, GeneratedPaths.Settings, GeneratedPaths.Terrain })
                GeneratedPaths.EnsureFolder(folder);

            ConfigureProject();
            SetupRenderPipeline();
            ConfigureImporters();
            Materials.CreateAll();
            PrefabGenerator.CreateAll();
            CreateBootScene();
            WorldSceneGenerator.Create();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(GeneratedPaths.BootScene, true),
                new EditorBuildSettingsScene(GeneratedPaths.WorldScene, true),
            };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Generator] 완료 ({(DateTime.Now - started).TotalSeconds:0.0}초)");
        }

        /// <summary>플레이어 설정 중 빌드 종류와 무관한 값.</summary>
        private static void ConfigureProject()
        {
            PlayerSettings.companyName = "Nimbus Games";
            PlayerSettings.productName = "Ashveil";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.muteOtherAudioSources = false;

            // 입력은 legacy Input Manager 를 쓴다(decisions.md D-10). 0 = Input Manager.
            var projectSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (projectSettings != null)
            {
                var so = new SerializedObject(projectSettings);
                var input = so.FindProperty("activeInputHandler");
                if (input != null)
                {
                    input.intValue = 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            QualitySettings.vSyncCount = 0;
            QualitySettings.shadowDistance = 40f;
        }

        /// <summary>URP 파이프라인 에셋을 만들어 그래픽·품질 설정에 지정한다(URP API는 리플렉션으로 호출해 컴파일 의존을 없앤다).</summary>
        private static void SetupRenderPipeline()
        {
            var path = GeneratedPaths.Settings + "/AshveilURP.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (asset == null)
            {
                const string runtime = ", Unity.RenderPipelines.Universal.Runtime";
                var assetType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset" + runtime);
                var rendererType = Type.GetType("UnityEngine.Rendering.Universal.RendererType" + runtime);
                if (assetType == null || rendererType == null)
                {
                    Debug.LogWarning("[Generator] URP 패키지를 찾지 못했습니다. 기본 렌더 파이프라인으로 진행합니다.");
                    return;
                }
                const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                var createRenderer = assetType.GetMethod("CreateRendererAsset", statics);
                var create = assetType.GetMethod("Create", statics);
                var renderer = createRenderer.Invoke(null, new[] { path, Enum.Parse(rendererType, "UniversalRenderer"), (object)true, "Renderer" });
                asset = (RenderPipelineAsset)create.Invoke(null, new[] { renderer });
                AssetDatabase.CreateAsset(asset, path);

                var so = new SerializedObject(asset);
                SetFloat(so, "m_ShadowDistance", 40f);
                SetInt(so, "m_MainLightShadowmapResolution", 1024);
                SetBool(so, "m_SupportsHDR", false);
                SetInt(so, "m_MSAA", 1);
                SetFloat(so, "m_RenderScale", 1f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            GraphicsSettings.defaultRenderPipeline = asset;
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }

            // URP 전역 설정(셰이더 리소스 등)이 없으면 만든다.
            var globalType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings, Unity.RenderPipelines.Universal.Runtime");
            globalType?.GetMethod("Ensure", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.Invoke(null, new object[] { true });

            // URP가 켜진 뒤에 FBX를 다시 가져와야 내장 머티리얼이 URP Lit 으로 만들어진다.
            if (AssetDatabase.IsValidFolder(GeneratedPaths.Kenney))
                AssetDatabase.ImportAsset(GeneratedPaths.Kenney, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureImporters()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { GeneratedPaths.Icons }))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (importer.filterMode == FilterMode.Point && importer.textureCompression == TextureImporterCompression.Uncompressed) continue;
                importer.filterMode = FilterMode.Point; // 픽셀 아트 아이콘
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        private static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            var cam = camera.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            new GameObject("Boot", typeof(Presentation.BootController));
            EditorSceneManager.SaveScene(scene, GeneratedPaths.BootScene);
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.floatValue = value;
        }

        private static void SetInt(SerializedObject so, string name, int value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.intValue = value;
        }

        private static void SetBool(SerializedObject so, string name, bool value)
        {
            var p = so.FindProperty(name);
            if (p != null) p.boolValue = value;
        }
    }

    /// <summary>생성 콘텐츠가 쓰는 머티리얼(대체 도형, 바닥 아이템, 지형).</summary>
    public static class Materials
    {
        public static Shader Lit => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        public static void CreateAll()
        {
            Create("FallbackActor", new Color(0.6f, 0.6f, 0.7f));
            var ground = Create("GroundItem", new Color(1f, 0.8f, 0.2f));
            ground.EnableKeyword("_EMISSION");
            ground.SetColor("_EmissionColor", new Color(1f, 0.7f, 0.1f) * 1.5f);
            Create("TownStone", new Color(0.55f, 0.53f, 0.5f));
            EditorUtility.SetDirty(ground);
        }

        public static Material Create(string name, Color color)
        {
            var path = $"{GeneratedPaths.Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Lit);
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
