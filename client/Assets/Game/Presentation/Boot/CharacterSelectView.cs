// 캐릭터 선택(최대 3슬롯)·삭제·생성(이름·직업·성별) 화면
using System;
using System.Collections.Generic;
using Ashveil.Application;
using Ashveil.Domain;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using UnityEngine;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class CharacterSelectView : MonoBehaviour
    {
        private Action<long> _onSelect;
        private Action _onLogout;
        private RectTransform _slots;
        private Text _status;
        private List<CharacterSummaryDto> _characters = new List<CharacterSummaryDto>();

        public static CharacterSelectView Create(Transform parent, Action<long> onSelect, Action onLogout)
        {
            var root = Ui.Stretch(Ui.Node(parent, "CharacterSelectView"));
            var view = root.gameObject.AddComponent<CharacterSelectView>();
            view._onSelect = onSelect;
            view._onLogout = onLogout;
            view.Build(root);
            view.Reload();
            return view;
        }

        private void Build(RectTransform root)
        {
            var title = Ui.Label(root, "Title", "캐릭터 선택", 56, TextAnchor.MiddleCenter, UiColors.Gold);
            Ui.Place(Ui.Rect(title), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1000, 90));
            var account = Ui.Label(root, "AccountText", "계정: " + GameContext.Session.Username, 28, TextAnchor.MiddleLeft, UiColors.TextDim);
            Ui.Place(Ui.Rect(account), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -40), new Vector2(700, 50));

            _slots = Ui.Place(Ui.Node(root, "Slots"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(1500, 560));
            var layout = _slots.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 40;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            _status = Ui.Label(root, "StatusText", "", 28, TextAnchor.MiddleCenter, UiColors.TextDim);
            Ui.Place(Ui.Rect(_status), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1200, 60));

            var logout = Ui.Button(root, "LogoutButton", "로그아웃", () => _onLogout());
            Ui.Place(Ui.Rect(logout), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -30), new Vector2(240, 80));
        }

        public void Reload()
        {
            _status.text = "캐릭터 목록을 불러오는 중...";
            AsyncUi.Run(async () =>
            {
                var list = await GameContext.Api.ListCharactersAsync();
                if (this == null) return;
                _characters = list;
                _status.text = "";
                RenderSlots();
            }, e =>
            {
                if (this != null) _status.text = e.Message;
            });
        }

        private void RenderSlots()
        {
            Ui.Clear(_slots);
            for (var i = 0; i < NameRules.MaxCharactersPerAccount; i++)
            {
                var slot = Ui.Panel(_slots, "CharacterSlot" + i, UiColors.Panel);
                if (i < _characters.Count) RenderCharacter(slot.transform, _characters[i]);
                else RenderEmpty(slot.transform);
            }
        }

        private void RenderCharacter(Transform slot, CharacterSummaryDto c)
        {
            var name = Ui.Label(slot, "NameText", c.Name, 44, TextAnchor.MiddleCenter, UiColors.Text);
            Ui.Place(Ui.Rect(name), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(440, 70));
            var info = Ui.Label(slot, "InfoText",
                $"{AlignmentView.ClassLabel(c.ClassType)} · Lv.{c.Level}\n{ZoneMap.DisplayName(c.Zone)}", 30, TextAnchor.MiddleCenter, UiColors.TextDim);
            Ui.Place(Ui.Rect(info), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(440, 110));
            var select = Ui.Button(slot, "SelectButton", "게임 시작", () => _onSelect(c.Id), UiColors.ButtonAccent, 34);
            Ui.Place(Ui.Rect(select), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 140), new Vector2(380, 100));
            var delete = Ui.Button(slot, "DeleteButton", "삭제", () => ConfirmDelete(c), UiColors.ButtonDanger, 26);
            Ui.Place(Ui.Rect(delete), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(200, 70));
        }

        private void RenderEmpty(Transform slot)
        {
            var create = Ui.Button(slot, "CreateSlotButton", "+ 새 캐릭터", OpenCreatePanel, UiColors.Button, 36);
            Ui.Place(Ui.Rect(create), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(380, 120));
        }

        private void ConfirmDelete(CharacterSummaryDto c)
        {
            ConfirmDialog.Show(transform, $"'{c.Name}' 캐릭터를 삭제할까요?\n삭제하면 되돌릴 수 없습니다.", "삭제", () =>
                AsyncUi.Run(async () =>
                {
                    await GameContext.Api.DeleteCharacterAsync(c.Id);
                    Toast.Show("캐릭터를 삭제했습니다.");
                    if (this != null) Reload();
                }));
        }

        private void OpenCreatePanel()
        {
            var window = Ui.Modal(transform, "CharacterCreatePanel", new Vector2(1200, 760), out var backdrop);
            var title = Ui.Label(window, "Title", "새 캐릭터 만들기", 44, TextAnchor.MiddleCenter, UiColors.Gold);
            Ui.Place(Ui.Rect(title), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -30), new Vector2(1000, 70));

            var nameInput = Ui.Input(window, "NameInput", "이름 (한글·영문·숫자 2~12자)");
            Ui.Place(Ui.Rect(nameInput), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(900, 90));

            var classType = ClassType.Warrior;
            var gender = Gender.Male;
            var classButtons = new Dictionary<ClassType, Button>();
            var genderButtons = new Dictionary<Gender, Button>();
            var description = Ui.Label(window, "ClassDescription", "", 28, TextAnchor.MiddleCenter, UiColors.TextDim);
            Ui.Place(Ui.Rect(description), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -360), new Vector2(1000, 80));

            void Refresh()
            {
                foreach (var pair in classButtons) pair.Value.image.color = pair.Key == classType ? UiColors.ButtonAccent : UiColors.Button;
                foreach (var pair in genderButtons) pair.Value.image.color = pair.Key == gender ? UiColors.ButtonAccent : UiColors.Button;
                description.text = Describe(classType);
            }

            var classes = new[] { ClassType.Warrior, ClassType.Archer, ClassType.Mage };
            for (var i = 0; i < classes.Length; i++)
            {
                var c = classes[i];
                var button = Ui.Button(window, "Class" + c, AlignmentView.ClassLabel(c), () => { classType = c; Refresh(); }, null, 34);
                Ui.Place(Ui.Rect(button), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - 1) * 300, -250), new Vector2(260, 90));
                classButtons[c] = button;
            }
            var genders = new[] { Gender.Male, Gender.Female };
            for (var i = 0; i < genders.Length; i++)
            {
                var g = genders[i];
                var button = Ui.Button(window, "Gender" + g, g == Gender.Male ? "남성" : "여성", () => { gender = g; Refresh(); }, null, 30);
                Ui.Place(Ui.Rect(button), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2((i - 0.5f) * 280, -470), new Vector2(240, 80));
                genderButtons[g] = button;
            }
            Refresh();

            var status = Ui.Label(window, "StatusText", "", 28, TextAnchor.MiddleCenter, UiColors.Bad);
            Ui.Place(Ui.Rect(status), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 140), new Vector2(1000, 60));

            Button createButton = null;
            createButton = Ui.Button(window, "CreateButton", "만들기", () =>
            {
                var name = nameInput.text.Trim();
                if (!NameRules.IsValidCharacterName(name))
                {
                    status.text = "이름은 2~12자의 한글, 영문, 숫자만 쓸 수 있습니다.";
                    return;
                }
                createButton.interactable = false;
                AsyncUi.Run(async () =>
                {
                    await GameContext.Api.CreateCharacterAsync(name, classType, gender);
                    Toast.Show($"'{name}' 캐릭터를 만들었습니다.");
                    if (backdrop != null) Destroy(backdrop.gameObject);
                    if (this != null) Reload();
                }, e =>
                {
                    if (status != null) status.text = e.Message;
                    if (createButton != null) createButton.interactable = true;
                });
            }, UiColors.ButtonAccent, 34);
            Ui.Place(Ui.Rect(createButton), new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(-15, 30), new Vector2(300, 100));
            var cancel = Ui.Button(window, "CancelButton", "취소", () => Destroy(backdrop.gameObject), null, 34);
            Ui.Place(Ui.Rect(cancel), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(15, 30), new Vector2(300, 100));
        }

        private static string Describe(ClassType c)
        {
            switch (c)
            {
                case ClassType.Archer: return "먼 거리에서 화살로 공격합니다. 스킬: 조준 사격, 화살비";
                case ClassType.Mage: return "강력한 마법과 회복 마법을 씁니다. 스킬: 화염구, 회복";
                default: return "튼튼한 근접 전투 직업입니다. 스킬: 강타, 대지 가르기";
            }
        }
    }
}
