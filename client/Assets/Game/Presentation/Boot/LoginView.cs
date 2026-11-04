// 로그인·회원가입 화면 (QA 빌드는 서버 주소 변경 패널 포함)
using System;
using Ashveil.Application;
using Ashveil.Shared;
using UnityEngine;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class LoginView : MonoBehaviour
    {
        private const string LastUsernameKey = "last_username";

        private InputField _username;
        private InputField _password;
        private Text _status;
        private Text _server;
        private Button _loginButton;
        private Button _signupButton;
        private Action _onLoggedIn;

        public static LoginView Create(Transform parent, Action onLoggedIn)
        {
            var root = Ui.Stretch(Ui.Node(parent, "LoginView"));
            var view = root.gameObject.AddComponent<LoginView>();
            view._onLoggedIn = onLoggedIn;
            view.Build(root);
            return view;
        }

        private void Build(RectTransform root)
        {
            var title = Ui.Label(root, "Title", "ASHVEIL", 110, TextAnchor.MiddleCenter, UiColors.Gold);
            title.fontStyle = FontStyle.Bold;
            Ui.Place(Ui.Rect(title), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1200, 140));
            var subtitle = Ui.Label(root, "Subtitle", "애쉬베일", 40, TextAnchor.MiddleCenter, UiColors.TextDim);
            Ui.Place(Ui.Rect(subtitle), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -230), new Vector2(1200, 60));

            var form = Ui.Panel(root, "Form", UiColors.Panel);
            Ui.Place(Ui.Rect(form), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(820, 520));

            _username = Ui.Input(form.transform, "UsernameInput", "아이디 (영문 소문자·숫자 4~16자)");
            Ui.Place(Ui.Rect(_username), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(700, 90));
            _username.text = PlayerPrefs.GetString(LastUsernameKey, "");

            _password = Ui.Input(form.transform, "PasswordInput", "비밀번호 (6자 이상)", password: true);
            Ui.Place(Ui.Rect(_password), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(700, 90));

            _loginButton = Ui.Button(form.transform, "LoginButton", "로그인", OnLogin, UiColors.ButtonAccent, 34);
            Ui.Place(Ui.Rect(_loginButton), new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(-10, -280), new Vector2(340, 100));
            _signupButton = Ui.Button(form.transform, "SignupButton", "회원가입", OnSignup, null, 34);
            Ui.Place(Ui.Rect(_signupButton), new Vector2(0.5f, 1), new Vector2(0, 1), new Vector2(10, -280), new Vector2(340, 100));

            _status = Ui.Label(form.transform, "StatusText", "", 28, TextAnchor.MiddleCenter, UiColors.Bad);
            Ui.Place(Ui.Rect(_status), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(760, 90));

            _server = Ui.Label(root, "ServerText", "", 24, TextAnchor.MiddleLeft, UiColors.TextDim);
            Ui.Place(Ui.Rect(_server), new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 20), new Vector2(1200, 50));
            RefreshServerLabel();

#if QA_BUILD
            var serverButton = Ui.Button(root, "ServerSettingsButton", "서버 설정", OpenServerSettings, null, 26);
            Ui.Place(Ui.Rect(serverButton), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 20), new Vector2(240, 70));
#endif
            var version = Ui.Label(root, "VersionText", "v" + UnityEngine.Application.version, 24, TextAnchor.MiddleRight, UiColors.TextDim);
            Ui.Place(Ui.Rect(version), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -20), new Vector2(400, 50));
        }

        private void RefreshServerLabel() =>
            _server.text = "서버: " + GameContext.Settings.BaseUrl + " (" + GameContext.Settings.Source + ")";

        private bool ValidateInputs()
        {
            if (!NameRules.IsValidUsername(_username.text))
            {
                _status.text = "아이디는 4~16자의 영문 소문자, 숫자, 밑줄만 쓸 수 있습니다.";
                return false;
            }
            if (!NameRules.IsValidPassword(_password.text))
            {
                _status.text = "비밀번호는 6~64자여야 합니다.";
                return false;
            }
            return true;
        }

        private void OnLogin()
        {
            if (!ValidateInputs()) return;
            SetBusy(true, "로그인 중...");
            AsyncUi.Run(async () =>
            {
                await GameContext.Api.LoginAsync(_username.text, _password.text);
                PlayerPrefs.SetString(LastUsernameKey, _username.text);
                if (this == null) return;
                _onLoggedIn();
            }, Fail);
        }

        private void OnSignup()
        {
            if (!ValidateInputs()) return;
            SetBusy(true, "계정을 만드는 중...");
            AsyncUi.Run(async () =>
            {
                await GameContext.Api.SignupAsync(_username.text, _password.text);
                await GameContext.Api.LoginAsync(_username.text, _password.text);
                PlayerPrefs.SetString(LastUsernameKey, _username.text);
                Toast.Show("계정을 만들었습니다.");
                if (this == null) return;
                _onLoggedIn();
            }, Fail);
        }

        private void Fail(ApiException e)
        {
            if (this == null) return;
            SetBusy(false, e.Message);
        }

        private void SetBusy(bool busy, string status)
        {
            _loginButton.interactable = !busy;
            _signupButton.interactable = !busy;
            _status.color = busy ? UiColors.TextDim : UiColors.Bad;
            _status.text = status;
        }

#if QA_BUILD
        private void OpenServerSettings()
        {
            var window = Ui.Modal(transform, "ServerSettingsPanel", new Vector2(1100, 420), out var backdrop);
            var title = Ui.Label(window, "Title", "서버 주소 (예: http://192.168.0.10:8080)", 30, TextAnchor.MiddleCenter);
            Ui.Place(Ui.Rect(title), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1000, 60));
            var input = Ui.Input(window, "ServerUrlInput", "http://<LAN IP>:8080");
            input.text = GameContext.Settings.BaseUrl;
            Ui.Place(Ui.Rect(input), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(980, 90));
            var save = Ui.Button(window, "SaveServerButton", "저장", () =>
            {
                if (!GameContext.Settings.Save(input.text))
                {
                    Toast.Show("주소 형식이 올바르지 않습니다.");
                    return;
                }
                RefreshServerLabel();
                Toast.Show("서버 주소를 저장했습니다.");
                Destroy(backdrop.gameObject);
            }, UiColors.ButtonAccent);
            Ui.Place(Ui.Rect(save), new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-15, 30), new Vector2(260, 90));
            var cancel = Ui.Button(window, "CancelButton", "닫기", () => Destroy(backdrop.gameObject));
            Ui.Place(Ui.Rect(cancel), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(15, 30), new Vector2(260, 90));
        }
#endif
    }
}
