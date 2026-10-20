// 컴파일 검사 전용 AltTester SDK 2.3.2 API 스텁 (실제 빌드에는 쓰지 않는다. 시그니처는 SDK 소스에서 옮김)
using UnityEditor;

namespace AltTester.AltTesterSDK.Driver
{
    [System.Serializable]
    public class AltInstrumentationSettings
    {
        public string AltServerHost = "127.0.0.1";
        public int AltServerPort = 13000;
        public string AppName = "__default__";
        public bool ResetConnectionData = false;
        public string UID = "";
        public bool hideGreenPopup = false;
        public bool SecureMode = false;
        public bool ShowNativePopup = false;
    }
}

namespace AltTester.AltTesterUnitySDK.Editor
{
    public class AltBuilder
    {
        public static void RemoveAltTesterFromScriptingDefineSymbols(BuildTargetGroup targetGroup) { }
        public static void AddAltTesterInScriptingDefineSymbolsGroup(BuildTargetGroup targetGroup) { }
        public static void InsertAltTesterInScene(string scene, AltTester.AltTesterSDK.Driver.AltInstrumentationSettings instrumentationSettings) { }
    }
}
