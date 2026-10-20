// 전투 HUD: 상태 막대·골드·구역·대상 정보, 공격·스킬·물약·자동사냥·PK·귀환 버튼, 각종 창 열기
using System.Linq;
using Ashveil.Application;
using Ashveil.Domain;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ashveil.Presentation
{
    public sealed class HudView : MonoBehaviour
    {
        private Canvas _canvas;
        private Text _name;
        private Text _level;
        private Text _gold;
        private Text _zone;
        private Text _alignment;
        private Bar _hp;
        private Bar _mp;
        private Bar _exp;
        private RectTransform _targetFrame;
        private Text _targetName;
        private Bar _targetHp;
        private Button _autoHunt;
        private Button _pkMode;
        private Text _potionCount;
        private Text[] _skillCooldownTexts;
        private VirtualJoystick _joystick;
        private RectTransform _panelLayer;

        private static PlayerState Player => GameContext.Player;
        private static PlayerController Controller => WorldServices.Player;

        public Transform PanelLayer => _panelLayer;

        public static HudView Create(Transform parent)
        {
            var canvas = Ui.CreateCanvas("HudCanvas", 10, parent);
            var hud = canvas.gameObject.AddComponent<HudView>();
            hud._canvas = canvas;
            hud.Build();
            Player.CharacterChanged += hud.RefreshStatus;
            Player.VitalsChanged += hud.RefreshVitals;
            Player.InventoryChanged += hud.RefreshInventoryBits;
            ZoneTracker.ZoneChanged += hud.OnZoneChanged;
            hud.RefreshStatus();
            hud.RefreshVitals();
            hud.RefreshInventoryBits();
            return hud;
        }

        private void OnDestroy()
        {
            if (Player != null)
            {
                Player.CharacterChanged -= RefreshStatus;
                Player.VitalsChanged -= RefreshVitals;
                Player.InventoryChanged -= RefreshInventoryBits;
            }
            ZoneTracker.ZoneChanged -= OnZoneChanged;
        }

        private void Build()
        {
            var root = Ui.Rect(_canvas);

            // 좌상단 상태
            var status = Ui.Panel(root, "StatusPanel", new Color(0, 0, 0, 0.45f));
            Ui.Place(status.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(560, 230));
            _name = Ui.Label(status.transform, "NameText", "", 34);
            Ui.Place(_name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -12), new Vector2(380, 46));
            _level = Ui.Label(status.transform, "LevelText", "", 30, TextAnchor.MiddleRight, UiColors.Gold);
            Ui.Place(_level.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -12), new Vector2(160, 46));
            _hp = Ui.Bar(status.transform, "HpBar", UiColors.Hp);
            Ui.Place(_hp.Root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -66), new Vector2(520, 40));
            _mp = Ui.Bar(status.transform, "MpBar", UiColors.Mp);
            Ui.Place(_mp.Root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -114), new Vector2(520, 34));
            _exp = Ui.Bar(status.transform, "ExpBar", UiColors.Exp);
            Ui.Place(_exp.Root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -156), new Vector2(520, 24));
            _alignment = Ui.Label(status.transform, "AlignmentText", "", 26);
            Ui.Place(_alignment.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -186), new Vector2(520, 36));

            // 상단 가운데: 골드·구역
            var top = Ui.Panel(root, "TopBar", new Color(0, 0, 0, 0.45f));
            Ui.Place(top.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(620, 70));
            var coin = Ui.Node(top.transform, "GoldIcon").gameObject.AddComponent<RawImage>();
            coin.texture = GameResources.Icon("gold");
            Ui.Place(coin.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, 0), new Vector2(48, 48));
            _gold = Ui.Label(top.transform, "GoldText", "0", 32, TextAnchor.MiddleLeft, UiColors.Gold);
            Ui.Place(_gold.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(76, 0), new Vector2(260, 60));
            _zone = Ui.Label(top.transform, "ZoneText", "", 28, TextAnchor.MiddleRight);
            Ui.Place(_zone.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(300, 60));

            // 대상 정보
            _targetFrame = Ui.Panel(root, "TargetFrame", new Color(0, 0, 0, 0.5f)).rectTransform;
            Ui.Place(_targetFrame, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(560, 90));
            _targetName = Ui.Label(_targetFrame, "TargetName", "", 28, TextAnchor.MiddleCenter);
            Ui.Place(_targetName.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(540, 40));
            _targetHp = Ui.Bar(_targetFrame, "TargetHpBar", UiColors.Hp);
            Ui.Place(_targetHp.Root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(520, 30));
            _targetFrame.gameObject.SetActive(false);

            // 미니맵 + 메뉴 버튼
            MinimapView.Create(root);
            var menu = Ui.Place(Ui.Node(root, "MenuButtons"), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -330), new Vector2(300, 90));
            var layout = menu.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            Ui.Button(menu, "InventoryButton", "가방", OpenInventory, null, 28);
            Ui.Button(menu, "CharacterButton", "정보", OpenCharacterInfo, null, 28);
#if QA_BUILD
            Ui.Button(menu, "GmButton", "GM", () => GmConsole.Open(_panelLayer), UiColors.ButtonDanger, 28);
#endif
            var exit = Ui.Button(root, "ExitButton", "나가기", ExitToCharacterSelect, null, 24);
            Ui.Place(Ui.Rect(exit), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -430), new Vector2(140, 60));

            // 조이스틱
            _joystick = VirtualJoystick.Create(root);

            // 우하단 행동 버튼
            RoundButton(root, "AttackButton", "공격", new Vector2(-60, 60), 230, OnAttack, UiColors.ButtonDanger);
            var skill1 = RoundButton(root, "Skill1Button", Controller.Profile.Skill1.Name, new Vector2(-320, 60), 150, () => Controller.UseSkill(0), UiColors.Button);
            var skill2 = RoundButton(root, "Skill2Button", Controller.Profile.Skill2.Name, new Vector2(-60, 320), 150, () => Controller.UseSkill(1), UiColors.Button);
            _skillCooldownTexts = new[] { CooldownLabel(skill1), CooldownLabel(skill2) };
            var potion = RoundButton(root, "PotionButton", "물약", new Vector2(-500, 60), 130, () => Controller.UsePotion(), new Color(0.55f, 0.15f, 0.2f));
            _potionCount = Ui.Label(potion.transform, "CountText", "0", 24, TextAnchor.LowerRight, UiColors.Text);
            Ui.Stretch(_potionCount.rectTransform, 0, 18, 0, 10);
            _autoHunt = RoundButton(root, "AutoHuntButton", "자동", new Vector2(-260, 300), 120, ToggleAutoHunt, UiColors.Button);
            _pkMode = RoundButton(root, "PkModeButton", "PK", new Vector2(-380, 230), 110, TogglePk, UiColors.Button);
            RoundButton(root, "TownButton", "귀환", new Vector2(-60, 500), 110, () => Controller.ReturnToTown(), new Color(0.2f, 0.45f, 0.35f));

            _panelLayer = Ui.Stretch(Ui.Node(root, "PanelLayer"));
        }

        private static Button RoundButton(Transform parent, string name, string label, Vector2 position, float size, System.Action onClick, Color color)
        {
            var button = Ui.Button(parent, name, label, onClick, color, size > 180 ? 40 : 26);
            button.image.sprite = Ui.Circle;
            Ui.Place(Ui.Rect(button), new Vector2(1, 0), new Vector2(1, 0), position, new Vector2(size, size));
            return button;
        }

        private static Text CooldownLabel(Button button)
        {
            var text = Ui.Label(button.transform, "CooldownText", "", 34, TextAnchor.MiddleCenter, UiColors.Gold);
            Ui.Stretch(text.rectTransform);
            return text;
        }

        private void Update()
        {
            var controller = Controller;
            if (controller == null) return;
            controller.JoystickInput = _joystick.Value;
            for (var i = 0; i < _skillCooldownTexts.Length; i++)
            {
                var cd = controller.SkillCooldowns[i];
                _skillCooldownTexts[i].text = cd > 0 ? Mathf.CeilToInt(cd).ToString() : "";
            }
            var target = controller.Target;
            var showTarget = target != null && !target.IsDead && target.isActiveAndEnabled;
            if (_targetFrame.gameObject.activeSelf != showTarget) _targetFrame.gameObject.SetActive(showTarget);
            if (showTarget)
            {
                _targetName.text = $"Lv.{target.Level} {target.DisplayName}";
                _targetName.color = target.Team == Team.Bot ? UiColors.FromHex(AlignmentView.ColorHex(target.Alignment)) : UiColors.Text;
                _targetHp.Set((float)target.Hp / target.MaxHp, $"{target.Hp} / {target.MaxHp}");
            }
            if (Time.frameCount % 30 == 0) RefreshAlignment();
        }

        private void RefreshStatus()
        {
            var c = Player.Character;
            if (c == null) return;
            _name.text = $"{c.Name} <size=24><color=#AAB0BB>{AlignmentView.ClassLabel(c.ClassType)}</color></size>";
            _level.text = "Lv." + c.Level;
            _gold.text = c.Gold.ToString("N0");
            var ratio = c.ExpToNext > 0 ? (float)c.Exp / c.ExpToNext : 0f;
            _exp.Set(ratio, $"EXP {ratio * 100f:0.0}%");
            RefreshAlignment();
            RefreshVitals();
        }

        private void RefreshAlignment()
        {
            var c = Player.Character;
            if (c == null) return;
            var state = AlignmentRules.GetState(c.Alignment.Value, c.Alignment.PurpleUntil, System.DateTime.UtcNow);
            _alignment.text = $"성향 {c.Alignment.Value} ({AlignmentView.Label(state)})";
            _alignment.color = UiColors.FromHex(AlignmentView.ColorHex(state));
            _name.color = _alignment.color;
        }

        private void RefreshVitals()
        {
            _hp.Set((float)Player.Hp / Player.MaxHp, $"HP {Player.Hp} / {Player.MaxHp}");
            _mp.Set((float)Player.Mp / Player.MaxMp, $"MP {Player.Mp} / {Player.MaxMp}");
        }

        private void RefreshInventoryBits()
        {
            var count = Player.CountOf(ItemCatalog.PotionHpSmall) + Player.CountOf(ItemCatalog.PotionHpLarge);
            _potionCount.text = count.ToString();
            _gold.text = Player.Gold.ToString("N0");
        }

        private void OnZoneChanged(ZoneId zone)
        {
            _zone.text = ZoneMap.DisplayName(zone);
            _zone.color = ZoneMap.IsSafeZone(zone) ? UiColors.Good : UiColors.Text;
        }

        private void OnAttack()
        {
            var controller = Controller;
            if (controller.Target != null && controller.CanAttack(controller.Target, out _)) return;
            var range = Mathf.Max(12f, controller.Profile.AttackRange + 4f);
            var target = Actor.Nearest(controller.Actor.Flat, range, a =>
                a.isActiveAndEnabled && (a.Team == Team.Monster || (a.Team == Team.Bot && controller.PkMode)) && controller.CanAttack(a, out _));
            if (target == null)
            {
                Toast.Show("주변에 공격할 대상이 없습니다.");
                return;
            }
            controller.SetTarget(target);
        }

        private void ToggleAutoHunt()
        {
            Controller.AutoHunt = !Controller.AutoHunt;
            _autoHunt.image.color = Controller.AutoHunt ? UiColors.ButtonAccent : UiColors.Button;
            Toast.Show(Controller.AutoHunt ? "자동사냥을 시작합니다." : "자동사냥을 멈춥니다.");
        }

        private void TogglePk()
        {
            Controller.PkMode = !Controller.PkMode;
            _pkMode.image.color = Controller.PkMode ? UiColors.ButtonDanger : UiColors.Button;
            Toast.Show(Controller.PkMode ? "PK 모드: 다른 캐릭터를 공격할 수 있습니다." : "PK 모드를 껐습니다.");
            if (!Controller.PkMode && Controller.Target != null && Controller.Target.Team == Team.Bot) Controller.ClearTarget();
        }

        public void SyncToggles()
        {
            _autoHunt.image.color = Controller.AutoHunt ? UiColors.ButtonAccent : UiColors.Button;
            _pkMode.image.color = Controller.PkMode ? UiColors.ButtonDanger : UiColors.Button;
        }

        public void OpenInventory() => InventoryPanel.Open(_panelLayer);
        public void OpenCharacterInfo() => CharacterInfoPanel.Open(_panelLayer);
        public void OpenShop() => ShopPanel.Open(_panelLayer);
        public void OpenEnhance() => EnhancePanel.Open(_panelLayer);

        public void ShowDeath(string killerName, long expLost, GroundItemDto dropped)
        {
            SyncToggles();
            DeathPanel.Show(_panelLayer, killerName, expLost, dropped);
        }

        private static void ExitToCharacterSelect()
        {
            WorldServices.PositionSaver?.SaveNow();
            SceneManager.LoadScene(SceneNames.Boot);
        }
    }
}
