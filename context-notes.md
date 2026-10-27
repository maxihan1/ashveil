# 컨텍스트 노트

작업 중 내린 결정과 이유를 시간순으로 붙인다. 큰 설계 결정은 `docs/decisions.md` 에 ID를 붙여 옮긴다.

## 2026-10-21
- 작업 환경. 로컬에 dotnet 없음, Docker(4.8GB) 있음 → 서버 빌드·테스트는 `mcr.microsoft.com/dotnet/sdk:10.0` 컨테이너에서 돌린다.
- GitHub 비공개 저장소 `maxihan1/ashveil` 생성. 작업은 `worktree-game-dev` 브랜치, main 직접 푸시 금지 → PR로 합친다.
- Boss Room 원본 커밋 `1299ba4fc97d83b634e7d32a86c438ba668d7f9c`(2025-10-23), Unity `6000.0.52f1`. 이 버전에 맞춘다.
- Boss Room 캐릭터 애니메이터 `CharacterSetController` 파라미터. `Speed`(float), `Attack1`, `Attack2`, `HitReact1`, `FallDown`, `Dead`, `SkillHeal`, `Buff1` 등(trigger). 애니메이션 이벤트는 `OnAnimEvent` 하나 → 수신용 빈 메서드 컴포넌트가 필요하다.
- Boss Room 그래픽 프리팹(`Prefabs/CharGFX/CharacterGraphics/PlayerGraphics_*`, `ImpGraphics`, `VandalImpGraphics`, `BossGraphics`)에는 Boss Room 스크립트(ClientCharacter 등)가 붙어 있다. 스크립트를 복사하지 않으므로 "Missing Script" 로 남는다. 동작에는 영향이 없고, 생성기가 프리팹을 만들 때 `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` 로 정리한다.
- 스폰 좌표는 결정적 해시로 한 번 생성해 `SpawnTable` 에 리터럴로 넣었다(언어별 Random 구현 차이 회피).
- 서버 통합 테스트는 테스트 클래스마다 전용 DB(ashveil_test_xxx)를 만들고 지운다. 난수는 ControlledRandom 으로 주입해 드랍·강화 결과를 고정한다.
- 서버 설정(Jwt, Game, ConnectionString)은 DI 등록 시점이 아니라 첫 사용 시점에 읽는다. WebApplicationFactory 의 UseSetting 값이 Program 상단보다 늦게 들어오기 때문.
- 리프레시 토큰은 회전 방식 + 재사용 감지(계정 전체 폐기). 그래서 클라이언트 ApiClient 는 동시 401 에도 갱신을 한 번만 한다(EditMode 테스트로 확인).
- 물약 사용 API(use-item)가 초안에 없어 추가했다(D-27).
- Boss Room 플레이어 그래픽 프리팹에는 Animator 가 없다. 원본은 PlayerAvatar 쪽 Animator(CharacterSet.fbx 아바타 + 직업별 override controller)를 쓴다. 생성기가 그래픽 루트에 Animator 를 붙인다. 리그는 Generic(animationType 2).
- Boss Room 몬스터·보스 그래픽 프리팹에는 Animator 와 override controller 가 이미 있다.
- Kenney FBX 는 URP 가 켜진 뒤 다시 가져와야 내장 머티리얼이 URP Lit 으로 만들어진다(생성기에서 ForceUpdate 재임포트).
- URP 에셋은 리플렉션으로 UniversalRenderPipelineAsset.CreateRendererAsset/Create 를 호출해 만든다(컴파일 의존 없음, 패키지 소스 확인).
- AltTester 2.3.2 런타임 105개 파일 중 8개만 #if ALTTESTER 로 감싸져 있어, Release 제외를 위해 패키지를 QA 잡에서만 추가한다(D-29).
- 컴파일 하네스는 C# 9 로 고정해야 한다. 최신 C# 의 span 변환 규칙 때문에 Unity TestRunner 소스의 .Reverse() 가 void 메서드로 바뀌어 깨진다.
- GetComponent 결과에 ?? 를 쓰면 에디터의 가짜 null 을 통과시키므로 == null 로 확인한다.
- .meta 파일은 커밋하지 않는다. 생성물이 같은 CI 실행 안에서 GUID 를 참조하므로 문제가 없다(D-32).
- 런타임 리뷰(별도 리뷰어)에서 찾은 버그를 고쳤다. 조이스틱 pivot 좌표(북동쪽으로만 이동), Boss Room 몬스터 루트 배율 덮어쓰기(보스 1/3 크기), 토스트가 터치를 삼킴, 스크롤 목록 드래그 임계값, UI 뒤 월드 탭(실행 순서 의존 → 직접 UI 레이캐스트), 누락 중첩 프리팹 Shadow.prefab(가져오기 목록 추가 + 생성 실패 시 계속), 자연 회복 없음, 늦은 응답이 죽은 캐릭터를 되살림, JSON 파싱 예외 뒤 버튼 잠김, AnimEventReceiver 파일 분리(프리팹 직렬화).
- 이름표 높이·몬스터 콜라이더는 모델 렌더러 높이로 잰다(Boss Room 모델 배율이 제각각).
- 사용자 요청으로 저장소를 공개로 바꿨다. 공개 전 점검에서 비밀값은 없었다(개발용 기본값만). 커밋 작성자 이메일은 공개된다. 공개 저장소는 Actions 사용 시간이 무료이고, 아티팩트(APK)는 GitHub 에 로그인한 누구나 받을 수 있다.
- QA APK 에는 AltTester SDK(GPL-3.0)가 들어간다. AltTester 문서는 SDK 가 든 앱을 배포하면 그 앱에도 GPL-3.0 을 적용해야 한다고 한다. 공개 아티팩트도 배포에 해당하므로 처리 방법을 사용자에게 물었다(답변 대기).
- Unity Personal 의 `.ulf` 경로가 막혀서 계정 로그인 방식으로 바꿨다(D-34). GameCI cli 의 personal 방식은 2026-09 에 나온 새 기능이라, 첫 실행 로그에서 활성화 단계(Licensing method: personal)를 먼저 확인한다.
- 사용자가 GPL-3.0 LICENSE 추가를 골랐다(D-35). 첫 GameCI 테스트 실행에서 Personal 활성화, PlayMode 4개, EditMode 27개가 통과했다. 테스트 액션의 coverageEnabled: false 입력은 game-ci CLI 가 거부해서 GAME_CI_COVERAGE_ENABLED 환경 변수로 바꿨다.
- 폰 첫 플레이 피드백 반영. 자동사냥이 마을에서 아무것도 하지 않던 것과 경비병 대상에 묶이던 것이 원인이었다. 이동 6→8.5, 카메라 높이 13→19.
- HUD 는 모바일 MMORPG 관례대로 스킬·물약·귀환을 하단 가운데 퀵슬롯으로 옮겼다. 창(가방·정보)은 모달이라 퀵슬롯 위를 덮는 것이 정상이다.
- 사용자 요청으로 어드민 API·백오피스·서버 스폰 표를 만들었다(D-36~D-38). GM API 는 본인 캐릭터 전용이라 그대로 둔다.
- CI 디버그 서명 키가 빌드마다 달라 새 QA APK 를 설치하려면 기존 앱을 지워야 한다(INSTALL_FAILED_UPDATE_INCOMPATIBLE). 고정하려면 QA 용 키스토어를 Secrets 에 넣는다.
- Release 빌드에서 UnityLinker 가 한 번 세그폴트로 실패했고, 같은 설정의 이전 빌드는 성공했다. 일시 오류로 보고 재실행했다.
- QA 서명 키스토어를 만들어 Secrets(ANDROID_QA_*)에 넣었다. Release 와 분리해서 QA 키가 Release 서명에 쓰이지 않게 했다. 원본과 비밀번호는 Mac 의 ~/.config/ashveil/qa-signing/ (권한 600). 이 키로 처음 설치할 때는 기존 앱을 한 번 지워야 한다.
- QA 서명 검증(빌드 39547973661): QA APK 인증서 SHA-256 이 QA 키스토어와 같다(9ED3…DDAA). Release 는 디버그 서명. 공개 빌드 로그에 비밀번호·키스토어 평문이 없는 것도 확인했다.
- 2026-10-23 QA 빌드 계측을 AltTester 에서 control_cli SDK 로 바꿨다(D-39). AltTester 와 control_cli 는 둘 다 `Input` 을 바꿔치기해 같이 넣을 수 없다. SDK 는 QA 잡의 `control-cli instrument` 가 커밋을 고정해 더한다(`unity-ci.yml` 의 `CONTROL_CLI_REF`).
- 2026-10-24 QA 빌드 잡 끝에 QA APK 를 QA Hub 에 올리는 단계를 더했다(D-40). versionCode 는 이미 `-buildNumber ${{ github.run_number }}` 라 버전 규칙은 그대로다. Secret `QAHUB_TOKEN` · `QAHUB_URL` 과 Variable `QAHUB_ENV` 를 넣기 전까지는 경고만 남기고 건너뛴다.
- 2026-10-25 QA Hub 시험(GAME-F1-14) 버그 4건을 고쳤다. 강화 두 번 누르기는 강화 창이 응답 대기 중에는 이미 버튼을 막고 있어서, 실제로는 첫 응답 뒤 두 번째 요청이 들어간 것이다. 서버가 두 요청을 가를 단서는 시간뿐이라 캐릭터별 0.5초 가드(`Game:Enhance:RepeatGuardMilliseconds`, 직전 `enhance_histories` 시각 기준)를 넣었다. 요청 키(idempotency key) 방식은 두 번 누르면 키도 둘이라 이 경우를 못 막는다. 기획서에 없는 「오류 추정」 규칙이다.
- 머리 위 이름표는 기획서 §9 를 따라 「이름 Lv.N」 으로 바꿨다(몬스터·플레이어도 같은 함수). HUD 대상 이름 칸은 기획서 규칙이 없어 「Lv.N 이름」 으로 남아 있다.
- 디바이스 팜은 폰을 놓을 때 앱 데이터를 지우므로 QA Hub 자동 실행에서는 빌드 기본값(`GAME_SERVER_URL`)만 쓰인다.
- 2026-10-27 QA 대상 게임 서버를 운영 서버 `/opt/ashveil` 로 옮긴다(OPS-F4-06b, D-41). 서버가 공개 저장소를 `/opt/ashveil/src` 에 clone 해 빌드하고, 비밀값은 checkout 밖 `/opt/ashveil/.env` 에 둔다(빌드 컨텍스트 · git 에 안 들어간다). 포트는 Auth_platform `infra/edge/README.md` 포트 표의 `18081`.
- `deploy.sh` 는 `git pull` 을 하지 않는다 — 도는 중인 스크립트 파일이 pull 로 바뀌면 bash 가 바뀐 자리부터 읽는다. `.env` 없이 DB 볼륨만 있으면 멈춘다(새 비밀번호가 볼륨 안 role 과 어긋난다).
- 맥 시험: 프로젝트 이름이 맥 개발 compose 와 같은 `ashveil` 이라 `COMPOSE_PROJECT_NAME=ashveil-prodtest` 로 돌렸다(같은 이름이면 맥의 `ashveil-db-1` 을 다시 만든다). health 200 · `environment` Qa · 어드민 키 없음 401 · 있음 200 · DB 포트 없음 · 덤프 TABLE DATA 13건 · 재실행 때 `.env` 그대로 · `.env` 지우면 ❌ 로 멈춤.
- 2026-10-27 OPS-F4-06b 서버 반영. `/opt/ashveil/src` clone(0b23400) → `deploy.sh` 첫 실행 `✅ … {"status":"ok","environment":"Qa"}` · `.env` 600 · ashveil-postgres 포트 없음. host Caddy 는 Auth_platform main 판(game 블록, PR #23)을 F4-07 과 한 번에 올렸다. 백업 한 번 `ashveil.dump 28K · TABLE DATA 13건` · `files 3개(… /opt/ashveil/.env)`.
- 바깥 확인(맥): `https://game.maxihan.com/health` 200 · Let's Encrypt(~2027-01-25) · `/admin` 키 없음 401 · 옛 기본키 401 · 가입 201(accountId 1) · 로그인 토큰. 확인용 계정 `opscheck1345` 가 남아 있다(백오피스 「테스트 계정 정리」로 지워도 된다).
- QA APK 빌드 47(run 40442106970) — 로그 `[Build] 기본 서버 주소: https://game.maxihan.com` (qa · release). 이제 HTTPS 라 Release 빌드도 이 서버로 시험할 수 있다.
- QA Hub 쪽에 넘길 것(OPS-F4-07 「대상 서버 주소를 새 도메인으로」): Ashveil 대상 서버 줄 주소 `https://game.maxihan.com` · 어드민 키는 서버 `/opt/ashveil/.env` 의 `ADMIN_KEY`(대화 · 로그에 찍지 않는다) · 새 DB 라 그 줄의 게임 계정은 가입 API 로 다시 만든다. 맥 Ashveil 컨테이너(`ashveil-db-1` · `ashveil-server-1`, 이미 멈춤)는 OPS-F4-08 이 정리한다.
