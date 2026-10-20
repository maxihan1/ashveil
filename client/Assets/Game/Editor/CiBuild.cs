// CI(GameCI -executeMethod) 빌드 진입점: 콘텐츠 생성 → QA/Release 분기 설정 → Android APK 빌드
using System;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ashveil.Editor
{
    public static class CiBuild
    {
        public const string QaDefine = "QA_BUILD";

        /// <summary>QA 빌드 계측(AltTester). com.alttester.sdk 패키지가 있을 때만 Ashveil.Editor.AltTester 어셈블리가 등록한다.</summary>
        public static Action<string, BuildArgs> QaInstrumentation;

        public sealed class BuildArgs
        {
            public string Variant = "qa";
            public string OutputPath;
            public int BuildNumber = 1;
            public string ServerUrl;
            public string AltHost = "127.0.0.1";
            public int AltPort = 13000;
            public string AltAppName = "ashveil";
            public string KeystoreName;
            public string KeystorePass;
            public string KeyaliasName;
            public string KeyaliasPass;

            public bool IsQa => Variant == "qa";

            /// <summary>-이름 값 형태의 명령줄 인자를 읽는다(GameCI 가 넘기는 -customBuildPath, -androidKeystore* 포함).</summary>
            public static BuildArgs Parse(string[] argv)
            {
                string Get(string name)
                {
                    var i = Array.IndexOf(argv, "-" + name);
                    return i >= 0 && i + 1 < argv.Length && !argv[i + 1].StartsWith("-", StringComparison.Ordinal) ? argv[i + 1] : null;
                }
                var args = new BuildArgs
                {
                    Variant = (Get("buildVariant") ?? "qa").ToLowerInvariant(),
                    OutputPath = Get("customBuildPath"),
                    // CI 는 값이 없을 때 자리 표시자 none 을 넘긴다(빈 값이면 다음 인자가 밀려 들어오므로).
                    ServerUrl = string.Equals(Get("serverUrl"), "none", StringComparison.OrdinalIgnoreCase) ? null : Get("serverUrl"),
                    AltHost = Get("altHost") ?? "127.0.0.1",
                    AltAppName = Get("altAppName") ?? "ashveil",
                    KeystoreName = Get("androidKeystoreName"),
                    KeystorePass = Get("androidKeystorePass"),
                    KeyaliasName = Get("androidKeyaliasName"),
                    KeyaliasPass = Get("androidKeyaliasPass"),
                };
                if (int.TryParse(Get("buildNumber"), out var number)) args.BuildNumber = Math.Max(1, number);
                if (int.TryParse(Get("altPort"), out var port)) args.AltPort = port;
                if (string.IsNullOrEmpty(args.OutputPath)) args.OutputPath = $"Builds/Android/Ashveil-{args.Variant}.apk";
                if (args.Variant != "qa" && args.Variant != "release") throw new ArgumentException("-buildVariant 는 qa 또는 release 여야 합니다: " + args.Variant);
                return args;
            }
        }

        /// <summary>GameCI buildMethod: Ashveil.Editor.CiBuild.BuildAndroid</summary>
        public static void BuildAndroid() => Build(BuildArgs.Parse(Environment.GetCommandLineArgs()));

        [MenuItem("Ashveil/Build/Android QA")]
        public static void BuildQaFromMenu() => Build(new BuildArgs { Variant = "qa", OutputPath = "Builds/Android/Ashveil-qa.apk" });

        [MenuItem("Ashveil/Build/Android Release")]
        public static void BuildReleaseFromMenu() => Build(new BuildArgs { Variant = "release", OutputPath = "Builds/Android/Ashveil-release.apk" });

        public static void Build(BuildArgs args)
        {
            Debug.Log($"[Build] {args.Variant} 빌드 #{args.BuildNumber} → {args.OutputPath}");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            ContentGenerator.GenerateAll();

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, args.IsQa ? "com.nimbusgames.ashveil.qa" : "com.nimbusgames.ashveil");
            PlayerSettings.productName = args.IsQa ? "Ashveil QA" : "Ashveil";
            PlayerSettings.bundleVersion = "0.1." + args.BuildNumber;
            PlayerSettings.Android.bundleVersionCode = args.BuildNumber;
            // HTTP 평문 통신은 QA 빌드에서만 허용한다(LAN 의 HTTP 서버용). Release 는 HTTPS 서버가 필요하다.
            PlayerSettings.insecureHttpOption = args.IsQa ? InsecureHttpOption.AlwaysAllowed : InsecureHttpOption.NotAllowed;
            PlayerSettings.SetScriptingDefineSymbols(android, args.IsQa ? QaDefine : "");
            EditorUserBuildSettings.buildAppBundle = false;
            ConfigureSigning(args);
            WriteServerConfig(args.ServerUrl);

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (args.IsQa)
            {
                if (QaInstrumentation == null)
                    throw new InvalidOperationException("QA 빌드에는 AltTester SDK(com.alttester.sdk)가 필요합니다. tools/ci/enable-alttester.py 로 패키지를 추가하세요.");
                QaInstrumentation(scenes[0], args);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args.OutputPath)) ?? ".");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                locationPathName = args.OutputPath,
                options = args.IsQa ? BuildOptions.Development : BuildOptions.None,
            });
            var summary = report.summary;
            Debug.Log($"[Build] 결과 {summary.result}, 크기 {summary.totalSize / (1024 * 1024)}MB, 오류 {summary.totalErrors}, 경고 {summary.totalWarnings}, {summary.totalTime}");
            if (summary.result != BuildResult.Succeeded)
                throw new Exception($"Android {args.Variant} 빌드 실패: {summary.result} (오류 {summary.totalErrors}개)");
        }

        private static void ConfigureSigning(BuildArgs args)
        {
            var custom = !string.IsNullOrEmpty(args.KeystoreName) && File.Exists(args.KeystoreName);
            PlayerSettings.Android.useCustomKeystore = custom;
            if (!custom)
            {
                Debug.Log("[Build] 키스토어가 없어 디버그 서명으로 빌드합니다.");
                return;
            }
            PlayerSettings.Android.keystoreName = Path.GetFullPath(args.KeystoreName);
            PlayerSettings.Android.keystorePass = args.KeystorePass;
            PlayerSettings.Android.keyaliasName = args.KeyaliasName;
            PlayerSettings.Android.keyaliasPass = args.KeyaliasPass;
        }

        /// <summary>빌드 기본 서버 주소를 Resources/server-config.json 으로 넣는다(없으면 런타임 기본값).</summary>
        private static void WriteServerConfig(string url)
        {
            var path = GeneratedPaths.Resources + "/server-config.json";
            if (string.IsNullOrWhiteSpace(url))
            {
                AssetDatabase.DeleteAsset(path);
                return;
            }
            File.WriteAllText(path, "{\"baseUrl\":\"" + url.Trim() + "\"}");
            AssetDatabase.ImportAsset(path);
            Debug.Log("[Build] 기본 서버 주소: " + url);
        }
    }

    /// <summary>QA 빌드에서만 AndroidManifest 에 usesCleartextTraffic=true 를 넣는다.</summary>
    public sealed class AndroidCleartextPostprocessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
            if (!defines.Split(';').Contains(CiBuild.QaDefine)) return;
            var manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifest)) return;

            const string androidNs = "http://schemas.android.com/apk/res/android";
            var doc = new XmlDocument();
            doc.Load(manifest);
            if (!(doc.SelectSingleNode("/manifest/application") is XmlElement application))
            {
                application = doc.CreateElement("application");
                doc.DocumentElement?.AppendChild(application);
            }
            application.SetAttribute("usesCleartextTraffic", androidNs, "true");
            doc.Save(manifest);
            Debug.Log("[Build] QA 빌드: usesCleartextTraffic=true 추가 → " + manifest);
        }
    }
}
