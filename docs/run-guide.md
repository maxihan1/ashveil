# 실행 가이드 (서버 → 폰 → AltTester)

준비물은 Mac(Docker Desktop), 같은 Wi-Fi 의 Android 폰, GitHub Actions 가 만든 APK 다.

## 1. 서버 실행 (5분)

```bash
cd infra
cp .env.example .env          # 필요하면 JWT_KEY 를 바꾼다
docker compose up -d --build  # PostgreSQL + 게임 서버
curl http://localhost:8080/health   # {"status":"ok","environment":"Qa"}
```

- 브라우저에서 `http://localhost:8080/swagger` 를 열면 API 문서와 테스트 화면이 나온다.
- 기본 환경은 `Qa` 라서 GM API 가 켜져 있다. 운영과 같은 동작을 보려면 `.env` 의 `SERVER_ENVIRONMENT=Production`.
- 폰에서 접속할 주소는 Mac 의 LAN IP 다.
  ```bash
  ipconfig getifaddr en0      # 예: 192.168.0.10 → 서버 주소 http://192.168.0.10:8080
  ```
- 처음 실행하면 봇 20명(초급 8, 중급 10, 보스 지역 2)이 자동으로 만들어진다.
- 데이터를 지우고 처음부터 시작하려면 `docker compose down -v`.

## 2. APK 받기

1. GitHub 저장소 → Actions → `unity-ci` → 가장 최근 main 실행(또는 Run workflow 로 수동 실행).
2. Artifacts 에서 `Ashveil-qa-apk`(QA) 또는 `Ashveil-release-apk`(Release)를 내려받아 압축을 푼다.
3. 폰에 설치한다. QA 와 Release 는 번들 ID 가 달라서 함께 설치된다.
   ```bash
   adb install -r Ashveil-qa.apk
   ```

> Release 빌드는 HTTP 평문 통신이 막혀 있어 HTTPS 서버가 필요하다. LAN 의 HTTP 서버로 확인할 때는 QA 빌드를 쓴다.

## 3. 폰에서 서버 주소 지정 (셋 중 하나)

| 방법 | 언제 | 하는 법 |
|---|---|---|
| 로그인 화면 **서버 설정** 버튼 (QA 빌드) | 가장 쉬움 | `http://192.168.0.10:8080` 입력 → 저장. 화면 아래 `서버: ... (runtime)` 로 확인 |
| 런타임 설정 파일 | 여러 기기에 한꺼번에 | 아래 명령으로 파일을 넣는다 |
| 빌드 기본값 | APK 를 새로 만들 때 | 저장소 Variable `GAME_SERVER_URL` 또는 수동 실행 입력 `serverUrl` |

```bash
echo '{"baseUrl":"http://192.168.0.10:8080"}' > server-config.json
adb push server-config.json /sdcard/Android/data/com.nimbusgames.ashveil.qa/files/server-config.json
```

우선순위는 런타임 설정 파일 → 빌드 기본값 → `http://127.0.0.1:8080` 이다.

## 4. AltTester Desktop 연결 (QA 빌드)

- 사용 버전은 **AltTester Unity SDK 2.3.2** 다. AltTester Desktop 도 **2.x**(SDK 2.3.x 와 호환되는 버전)를 쓴다.
- QA 빌드는 기본으로 `127.0.0.1:13000` 의 AltTester 서버에 접속한다. USB 로 연결한 폰이면 포트를 Mac 으로 넘긴다.
  ```bash
  adb reverse tcp:13000 tcp:13000
  ```
- Wi-Fi 로만 연결하려면 빌드 Variable `ALTTESTER_HOST` 에 Mac 의 LAN IP 를 넣고 다시 빌드한다.
- 순서
  1. Mac 에서 AltTester Desktop 실행 (기본 포트 13000).
  2. 폰에서 Ashveil QA 실행. 첫 화면 상단의 AltTester 초록 팝업에 연결 상태가 뜬다.
  3. AltTester Desktop 의 앱 목록에 `ashveil` 이 보이면 선택 → 오브젝트 트리가 나온다.
- 여러 기기(Device Farm)를 동시에 붙이면 기기마다 앱 이름이 같아도 고유 ID로 구분된다. 묶음을 나누려면 `ALTTESTER_APP_NAME` 을 바꿔 빌드한다.
- 라이선스 메모. AltTester Lite(무료)는 GUI 1연결이다. CI·컨테이너에서 서버를 헤드리스로 돌리거나 여러 연결을 쓰려면 AltTester Pro 가 필요하다.

### 자주 찾는 오브젝트 이름

| 화면 | 오브젝트 |
|---|---|
| 로그인 | `UsernameInput`, `PasswordInput`, `LoginButton`, `SignupButton`, `StatusText`, `ServerSettingsButton` |
| 캐릭터 선택 | `CharacterSlot0..2` (`NameText`, `SelectButton`, `DeleteButton`, `CreateSlotButton`), `CharacterCreatePanel` (`NameInput`, `ClassWarrior`, `ClassArcher`, `ClassMage`, `GenderMale`, `GenderFemale`, `CreateButton`) |
| HUD | `GoldText`, `LevelText`, `HpBar`, `MpBar`, `ExpBar`, `AlignmentText`, `ZoneText`, `TargetFrame`, `AttackButton`, `Skill1Button`, `Skill2Button`, `PotionButton`, `AutoHuntButton`, `PkModeButton`, `TownButton`, `InventoryButton`, `CharacterButton`, `GmButton`, `Minimap`, `Joystick` |
| 월드 | `Player`, `Monster_<스폰ID>_<종류>`, `Bot_<ID>_<이름>`, `Npc_npc_merchant`, `Npc_npc_enhancer`, `Npc_npc_guard_*`, `GroundItem_<ID>` |
| 창 | `InventoryPanel`, `ItemDetailPopup`(`EquipButton`, `UnequipButton`, `UseButton`), `ShopPanel`(`BuyTab`, `SellTab`, `Buy_<아이템ID>`), `EnhancePanel`(`EnhanceButton`, `ResultText`), `CharacterInfoPanel`, `DeathPanel`(`RespawnButton`), `GmConsole` |

## 5. 문제 해결

| 증상 | 확인할 것 |
|---|---|
| 로그인 시 "서버에 연결할 수 없습니다" | 폰과 Mac 이 같은 Wi-Fi 인지, 서버 주소가 LAN IP 인지, Mac 방화벽이 8080 을 막지 않는지. 폰 브라우저로 `http://<IP>:8080/health` 를 열어 본다 |
| Release 빌드만 접속 실패 | 정상 동작이다. Release 는 HTTP 를 막는다 |
| AltTester 앱 목록이 비어 있음 | `adb reverse` 를 했는지, Desktop 포트가 13000 인지, QA 빌드인지 |
| 로그인 직후 다시 로그인 화면 | 서버를 `down -v` 로 초기화했거나 JWT_KEY 를 바꾸면 기존 토큰이 무효가 된다 |
