// 게임 전역 서비스(세션·API·플레이어 상태·서버 설정) 접근점
namespace Ashveil.Application
{
    public static class GameContext
    {
        public static Session Session { get; private set; }
        public static ApiClient Client { get; private set; }
        public static GameApi Api { get; private set; }
        public static PlayerState Player { get; private set; }
        public static ServerSettings Settings { get; private set; }

        public static bool IsInitialized => Api != null;

        public static void Initialize(ServerSettings settings, IHttpTransport transport)
        {
            Settings = settings;
            Session = new Session();
            Client = new ApiClient(transport, Session, () => Settings.BaseUrl);
            Api = new GameApi(Client, Session);
            Player = new PlayerState();
        }

        /// <summary>테스트·로그아웃용. 다음 Initialize 전까지 서비스가 비어 있다.</summary>
        public static void Shutdown()
        {
            Session = null;
            Client = null;
            Api = null;
            Player = null;
            Settings = null;
        }
    }
}
