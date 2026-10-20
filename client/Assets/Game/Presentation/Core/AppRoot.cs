// 앱 전역 루트: 서비스 초기화, 프레임 설정, 세션 만료 처리, 씬 이름 상수, 비동기 UI 실행 헬퍼
using System;
using System.Threading.Tasks;
using Ashveil.Application;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ashveil.Presentation
{
    public static class SceneNames
    {
        public const string Boot = "Boot";
        public const string World = "World";
    }

    public sealed class AppRoot : MonoBehaviour
    {
        private static AppRoot _instance;

        public static void EnsureExists()
        {
            if (_instance != null) return;
            new GameObject("AppRoot").AddComponent<AppRoot>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            UnityEngine.Application.targetFrameRate = 30;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (!GameContext.IsInitialized)
                GameContext.Initialize(ServerSettings.LoadForPlayer(), new UnityWebRequestTransport());
            GameContext.Client.SessionExpired += OnSessionExpired;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;
            if (GameContext.Client != null) GameContext.Client.SessionExpired -= OnSessionExpired;
            _instance = null;
        }

        private static void OnSessionExpired()
        {
            GameContext.Session.Clear();
            Toast.Show("로그인 정보가 만료되었습니다. 다시 로그인해 주세요.");
            if (SceneManager.GetActiveScene().name != SceneNames.Boot) SceneManager.LoadScene(SceneNames.Boot);
        }
    }

    public static class AsyncUi
    {
        /// <summary>UI 이벤트에서 비동기 작업을 실행하고, 실패하면 서버 안내 문구를 토스트로 띄운다.</summary>
        public static async void Run(Func<Task> action, Action<ApiException> onError = null)
        {
            try
            {
                await action();
            }
            catch (ApiException e)
            {
                if (onError != null) onError(e);
                else Toast.Show(e.Message);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Toast.Show("오류가 발생했습니다.");
            }
        }
    }
}
