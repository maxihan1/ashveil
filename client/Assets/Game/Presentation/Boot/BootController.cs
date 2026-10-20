// Boot 씬 진입점: 로그인 화면 → 캐릭터 선택·생성 화면 → World 씬 로드
using Ashveil.Application;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ashveil.Presentation
{
    public sealed class BootController : MonoBehaviour
    {
        private Canvas _canvas;
        private LoginView _login;
        private CharacterSelectView _select;

        private void Start()
        {
            AppRoot.EnsureExists();
            _canvas = Ui.CreateCanvas("BootCanvas", 0, transform);
            var background = Ui.Panel(_canvas.transform, "Background", new Color(0.05f, 0.06f, 0.09f, 1f));
            Ui.Stretch(Ui.Rect(background));

            if (GameContext.Session.IsLoggedIn) ShowCharacterSelect();
            else ShowLogin();
        }

        public void ShowLogin()
        {
            if (_select != null) Destroy(_select.gameObject);
            _login = LoginView.Create(_canvas.transform, ShowCharacterSelect);
        }

        public void ShowCharacterSelect()
        {
            if (_login != null) Destroy(_login.gameObject);
            _select = CharacterSelectView.Create(_canvas.transform, EnterWorld, () =>
            {
                GameContext.Session.Clear();
                ShowLogin();
            });
        }

        private static void EnterWorld(long characterId)
        {
            GameContext.Session.CharacterId = characterId;
            GameContext.Player.Reset();
            SceneManager.LoadScene(SceneNames.World);
        }
    }
}
