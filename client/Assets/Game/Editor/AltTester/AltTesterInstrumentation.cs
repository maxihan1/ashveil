// QA 빌드 계측: ALTTESTER 정의 심볼 추가 + 첫 씬에 AltTester 프리팹 삽입(서버 호스트·포트·앱 이름은 빌드 인자)
using AltTester.AltTesterSDK.Driver;
using AltTester.AltTesterUnitySDK.Editor;
using UnityEditor;
using UnityEngine;

namespace Ashveil.Editor
{
    [InitializeOnLoad]
    public static class AltTesterInstrumentation
    {
        static AltTesterInstrumentation()
        {
            CiBuild.QaInstrumentation = Instrument;
        }

        private static void Instrument(string firstScene, CiBuild.BuildArgs args)
        {
            AltBuilder.AddAltTesterInScriptingDefineSymbolsGroup(BuildTargetGroup.Android);
            AltBuilder.InsertAltTesterInScene(firstScene, new AltInstrumentationSettings
            {
                AltServerHost = args.AltHost,
                AltServerPort = args.AltPort,
                AppName = args.AltAppName,
                // 기기에 저장된 이전 접속 정보 대신 빌드 인자를 쓴다(Device Farm 기기마다 같은 값).
                ResetConnectionData = true,
            });
            Debug.Log($"[Build] AltTester 계측: {args.AltHost}:{args.AltPort}, 앱 이름 {args.AltAppName}");
        }
    }
}
