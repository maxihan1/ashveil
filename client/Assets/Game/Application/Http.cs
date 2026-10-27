// HTTP 전송 추상화와 UnityWebRequest 구현, 서버 JSON 직렬화 설정
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine.Networking;

namespace Ashveil.Application
{
    public sealed class HttpRequestData
    {
        public string Method;
        public string Url;
        public string Body;
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();
    }

    public sealed class HttpResponseData
    {
        public long Status;
        public string Body;
        /// <summary>연결 실패·타임아웃처럼 HTTP 응답 자체를 못 받은 경우.</summary>
        public bool NetworkError;
        public string Error;
    }

    /// <summary>테스트에서 가짜 서버를 끼울 수 있게 전송 계층만 분리한다.</summary>
    public interface IHttpTransport
    {
        Task<HttpResponseData> SendAsync(HttpRequestData request);
    }

    public sealed class UnityWebRequestTransport : IHttpTransport
    {
        public int TimeoutSeconds = 10;

        public Task<HttpResponseData> SendAsync(HttpRequestData request)
        {
            var completion = new TaskCompletionSource<HttpResponseData>();
            var web = new UnityWebRequest(request.Url, request.Method)
            {
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = TimeoutSeconds,
            };
            if (request.Body != null)
            {
                web.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(request.Body));
                web.SetRequestHeader("Content-Type", "application/json");
            }
            foreach (var header in request.Headers) web.SetRequestHeader(header.Key, header.Value);

            var operation = web.SendWebRequest();
            operation.completed += _ =>
            {
                var response = new HttpResponseData
                {
                    Status = web.responseCode,
                    Body = web.downloadHandler != null ? web.downloadHandler.text : null,
                    NetworkError = web.result == UnityWebRequest.Result.ConnectionError ||
                                   web.result == UnityWebRequest.Result.DataProcessingError,
                    Error = web.error,
                };
                web.Dispose();
                completion.SetResult(response);
            };
            return completion.Task;
        }
    }

    /// <summary>서버(System.Text.Json, camelCase, enum 문자열)와 맞춘 Newtonsoft 설정.</summary>
    public static class ClientJson
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters = { new StringEnumConverter() },
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        };

        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);

        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }
}
