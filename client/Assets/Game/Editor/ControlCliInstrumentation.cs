// 0단계 시험: QA 빌드 계측을 AltTester 대신 control_cli SDK 로(CONTROL_CLI 정의만 더한다)
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Ashveil.Editor
{
    [InitializeOnLoad]
    public static class ControlCliInstrumentation
    {
        static ControlCliInstrumentation()
        {
            CiBuild.QaInstrumentation = (scene, args) =>
            {
                var target = NamedBuildTarget.Android;
                PlayerSettings.SetScriptingDefineSymbols(target, PlayerSettings.GetScriptingDefineSymbols(target) + ";CONTROL_CLI");
                Debug.Log("[Build] control_cli 계측: CONTROL_CLI");
            };
        }
    }
}
