# 실행 가이드 (서버 → 폰 → 게임 조작)

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

### 백오피스 (어드민 웹)

- 브라우저에서 http://localhost:8080/backoffice 를 열고 `infra/.env` 의 `ADMIN_KEY` 값(기본 `local-qa-admin-key-change-me`)을 넣는다.
- 캐릭터 검색·골드/레벨/성향 변경·아이템 지급·초기화, 강화·드랍 확률, 결과 고정, 몬스터 스폰 편집, 테스트 계정 정리를 할 수 있다.
- 같은 기능을 API 로 쓰려면 `X-Admin-Key` 헤더를 붙여 `/admin/*` 를 부른다(`docs/api.md` 어드민 절).

### 운영 서버 `https://game.maxihan.com` (D-41)

QA Hub · 폰이 쓰는 대상 서버다. 운영 서버 `101.79.19.193` 의 `/opt/ashveil` 에서 돌고, host Caddy 가 `127.0.0.1:18081` 로 넘긴다(사이트 블록 원본은 Auth_platform `infra/edge/Caddyfile`). DB 는 포트를 열지 않는다.

```bash
# 처음 한 번 (서버 root)
git clone -q -b worktree-game-dev https://github.com/maxihan1/ashveil.git /opt/ashveil/src
# 배포마다 — .env 가 없으면 처음 한 번 만든다(JWT_KEY · ADMIN_KEY · DB_PASSWORD, openssl rand)
git -C /opt/ashveil/src pull --ff-only && /opt/ashveil/src/infra/prod/deploy.sh
```

- 비밀값은 `/opt/ashveil/.env`(600) 에만 있다. 백오피스 · QA Hub 대상 서버 줄의 어드민 키는 여기의 `ADMIN_KEY` 다.
- DB 는 매일 03:15 백업된다(Auth_platform `infra/backup/ops-backup.sh` 의 `ashveil-postgres:ashveil`).

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

디바이스 팜 · QA Hub 자동 실행은 빌드 기본값만 쓴다(팜이 폰을 놓을 때 앱 데이터와 함께 설정 파일을 지운다). 그래서 `GAME_SERVER_URL` 을 QA Hub 대상 서버 줄 주소와 같게 둔다(지금 `https://game.maxihan.com`). 바뀌면 변수도 바꾸고 QA APK 를 다시 만든다.

## 4. 게임 조작 SDK (control_cli, QA 빌드)

- QA 빌드는 폰 안 `127.0.0.1:13100` 에서 명령을 받는다(D-39). 폰 밖에서는 `adb forward` 로만 닿는다 — CLI 가 명령마다 건다.
- 순서
  1. `npm install -g github:maxihan1/control_cli`
  2. 폰에서 Ashveil QA 실행 → USB 로 Mac 에 잇는다.
  3. `control-cli ping` → `{"ok":true,"app":"com.nimbusgames.ashveil.qa",...}` 이 나오면 붙었다.
  4. `control-cli tree` 로 오브젝트 트리, `control-cli click LoginButton` 으로 누르기, `control-cli errors` 로 게임 오류, `control-cli layout` 으로 화면 잘림 검사.
- 폰이 여러 대면 `-s <시리얼>` 을 붙인다. 명령 · 응답 꼴은 [control_cli README](https://github.com/maxihan1/control_cli) 가 정본이다.

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
| `control-cli ping` 이 「게임 포트 13100 에 붙지 못했습니다」 | QA 빌드인지(Release 에는 SDK 가 없다), 앱이 앞에 켜져 있는지, `adb devices` 에 폰이 보이는지 |
| 로그인 직후 다시 로그인 화면 | 서버를 `down -v` 로 초기화했거나 JWT_KEY 를 바꾸면 기존 토큰이 무효가 된다 |
