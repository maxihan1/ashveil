// 서버 주소 결정: 런타임 설정 파일 > 빌드 기본값(Resources) > 고정 기본값
using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Ashveil.Application
{
    public sealed class ServerConfigFile
    {
        [JsonProperty("baseUrl")] public string BaseUrl { get; set; }
    }

    /// <summary>
    /// 1) persistentDataPath/server-config.json (QA가 adb push 하거나 로그인 화면에서 저장)
    /// 2) Resources/server-config.json (빌드할 때 -serverUrl 인자로 생성)
    /// 3) http://127.0.0.1:8080
    /// </summary>
    public sealed class ServerSettings
    {
        public const string FileName = "server-config.json";
        public const string ResourceName = "server-config";
        public const string FallbackUrl = "http://127.0.0.1:8080";

        private readonly string _runtimePath;
        public string BaseUrl { get; private set; }
        public string Source { get; private set; }

        public ServerSettings(string persistentDataPath, string buildDefaultJson)
        {
            _runtimePath = Path.Combine(persistentDataPath ?? "", FileName);
            var runtime = File.Exists(_runtimePath) ? Parse(SafeRead(_runtimePath)) : null;
            var build = Parse(buildDefaultJson);
            if (runtime != null) { BaseUrl = runtime; Source = "runtime"; }
            else if (build != null) { BaseUrl = build; Source = "build"; }
            else { BaseUrl = FallbackUrl; Source = "default"; }
        }

        public static ServerSettings LoadForPlayer()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            return new ServerSettings(UnityEngine.Application.persistentDataPath, asset != null ? asset.text : null);
        }

        /// <summary>로그인 화면에서 바꾼 주소를 런타임 설정 파일에 저장한다.</summary>
        public bool Save(string url)
        {
            var normalized = Normalize(url);
            if (normalized == null) return false;
            File.WriteAllText(_runtimePath, JsonConvert.SerializeObject(new ServerConfigFile { BaseUrl = normalized }));
            BaseUrl = normalized;
            Source = "runtime";
            return true;
        }

        public static string Normalize(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim();
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "http://" + url;
            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host) ? url.TrimEnd('/') : null;
        }

        private static string Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return Normalize(JsonConvert.DeserializeObject<ServerConfigFile>(json)?.BaseUrl);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static string SafeRead(string path)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) { return null; }
        }
    }
}
