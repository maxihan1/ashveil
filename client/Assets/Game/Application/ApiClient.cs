// 서버 REST 호출 공통 처리: 인증·캐릭터 헤더, 401 시 토큰 갱신(직렬화), 에러 코드 변환
using System;
using System.Threading.Tasks;
using Ashveil.Domain;
using Ashveil.Shared;
using Ashveil.Shared.Dto;

namespace Ashveil.Application
{
    public sealed class ApiException : Exception
    {
        public string Code { get; }
        public long Status { get; }

        public ApiException(string code, string message, long status) : base(message)
        {
            Code = code;
            Status = status;
        }
    }

    public sealed class Session
    {
        public string Username { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public long CharacterId { get; set; }

        public bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken);

        public void Apply(TokenResponse tokens)
        {
            AccessToken = tokens.AccessToken;
            RefreshToken = tokens.RefreshToken;
        }

        public void Clear()
        {
            AccessToken = null;
            RefreshToken = null;
            CharacterId = 0;
        }
    }

    public sealed class ApiClient
    {
        public const string CharacterHeader = "X-Character-Id";

        private readonly IHttpTransport _transport;
        private readonly Session _session;
        private readonly Func<string> _baseUrl;
        private Task<bool> _refreshing;

        /// <summary>토큰 갱신까지 실패해 다시 로그인해야 할 때.</summary>
        public event Action SessionExpired;

        public ApiClient(IHttpTransport transport, Session session, Func<string> baseUrl)
        {
            _transport = transport;
            _session = session;
            _baseUrl = baseUrl;
        }

        public string BaseUrl => _baseUrl().TrimEnd('/');

        public async Task<T> SendAsync<T>(string method, string path, object body = null, bool auth = true)
        {
            var response = await SendRawAsync(method, path, body, auth);
            if (response.Status == 401 && auth && !string.IsNullOrEmpty(_session.RefreshToken) && await RefreshOnceAsync())
                response = await SendRawAsync(method, path, body, auth);

            if (response.NetworkError)
                throw new ApiException(ErrorCodes.NetworkError, ErrorMessages.For(ErrorCodes.NetworkError), 0);

            if (response.Status >= 200 && response.Status < 300)
                return string.IsNullOrEmpty(response.Body) ? default : ClientJson.Deserialize<T>(response.Body);

            var error = ParseError(response.Body);
            var code = error?.Code ?? (response.Status == 401 ? ErrorCodes.Unauthorized : ErrorCodes.Internal);
            if (response.Status == 401 && auth) SessionExpired?.Invoke();
            var message = ErrorMessages.IsKnown(code) ? ErrorMessages.For(code) : error?.Message ?? ErrorMessages.For(code);
            throw new ApiException(code, message, response.Status);
        }

        public Task SendAsync(string method, string path, object body = null, bool auth = true) =>
            SendAsync<object>(method, path, body, auth);

        private Task<HttpResponseData> SendRawAsync(string method, string path, object body, bool auth)
        {
            var request = new HttpRequestData
            {
                Method = method,
                Url = BaseUrl + path,
                Body = body == null ? null : ClientJson.Serialize(body),
            };
            if (auth && !string.IsNullOrEmpty(_session.AccessToken)) request.Headers["Authorization"] = "Bearer " + _session.AccessToken;
            if (auth && _session.CharacterId > 0) request.Headers[CharacterHeader] = _session.CharacterId.ToString();
            return _transport.SendAsync(request);
        }

        // 동시에 여러 요청이 401을 받아도 갱신은 한 번만 한다(리프레시 토큰 재사용은 서버가 탈취로 간주한다).
        private Task<bool> RefreshOnceAsync()
        {
            if (_refreshing != null && !_refreshing.IsCompleted) return _refreshing;
            _refreshing = RefreshAsync();
            return _refreshing;
        }

        private async Task<bool> RefreshAsync()
        {
            var response = await _transport.SendAsync(new HttpRequestData
            {
                Method = "POST",
                Url = BaseUrl + "/auth/refresh",
                Body = ClientJson.Serialize(new RefreshRequest { RefreshToken = _session.RefreshToken }),
            });
            if (response.NetworkError || response.Status != 200) return false;
            _session.Apply(ClientJson.Deserialize<TokenResponse>(response.Body));
            return true;
        }

        private static ErrorResponse ParseError(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            try
            {
                return ClientJson.Deserialize<ErrorResponse>(body);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
