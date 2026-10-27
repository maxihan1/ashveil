# 설계 결정 기록

메타 프롬프트에서 모호하거나 충돌하는 부분을 이렇게 결정했다. 새 결정은 아래에 번호를 이어 붙인다.

| ID | 결정 | 근거 |
|---|---|---|
| D-01 | 게임 이름 **Ashveil(애쉬베일)**, 개발사 Nimbus Games, 번들 ID `com.nimbusgames.ashveil` (QA 빌드는 `com.nimbusgames.ashveil.qa`) | 리니지 고유명사 금지. QA와 Release를 한 폰에 같이 설치할 수 있게 번들 ID를 나눈다 |
| D-02 | 서버 런타임은 **.NET 10 (LTS)** | 2026-10 기준 .NET 8 지원이 2026-11에 끝난다. 최신 LTS가 상용 개발사의 합리적 선택 |
| D-03 | shared는 **netstandard2.1 + C# 9** 로 제한 | Unity 6 컴파일러가 C# 9까지 지원한다. record·init·file-scoped namespace 등은 쓰지 않는다 |
| D-04 | Unity는 shared를 **로컬 UPM 패키지**(`file:../../shared/Package`)로 참조 | 소스 복사 없이 한 벌의 코드를 공유한다. csproj의 bin/obj는 패키지 폴더 밖에 생겨 Unity가 읽지 않는다 |
| D-05 | Boss Room 아트는 저장소에 커밋하지 않고 **CI에서 고정 커밋을 받아 복사**(`tools/fetch-bossroom.sh`) | 원본이 Git LFS 대용량이다. 우리 저장소 LFS 용량·대역폭을 쓰지 않고, 고정 커밋으로 재현성을 지킨다. 코드(Scripts), Netcode·UGS 패키지는 복사하지 않는다 |
| D-06 | 씬·프리팹은 CI에서 매번 생성하고 **생성물은 커밋하지 않는다** | 로컬 Editor가 없어 생성물을 검토할 수 없다. 생성 코드가 진실 원천이다 |
| D-07 | 테스트 폴더는 `client/Assets/Tests/EditMode`, `client/Assets/Tests/PlayMode` | Unity는 Assets 밖 스크립트를 컴파일하지 않는다 |
| D-08 | UI는 프리팹 대신 **런타임 코드 생성**(uGUI) | YAML 직렬화 실수 위험이 없고, AltTester 오브젝트 이름이 코드와 일치한다 |
| D-09 | 텍스트는 legacy `UnityEngine.UI.Text` + **Noto Sans KR(OFL)** 폰트 | TextMeshPro는 batchmode에서 TMP Essential Resources 임포트가 번거롭다. 한글 글리프를 확실히 보장하려면 폰트를 포함해야 한다. 폰트는 CC0가 아니라 OFL이며 ThirdPartyNotices에 고지한다 |
| D-10 | 입력은 **legacy Input Manager**(터치) | AltTester와 Boss Room 아트에 Input System 패키지가 필요 없다. 패키지를 줄이면 CI 임포트 시간과 실패 지점이 준다 |
| D-11 | 이동은 NavMesh 없이 **Terrain 높이 샘플링 + 직선 조향** | 야외 필드라 장애물 회피 요구가 낮다. NavMesh 베이크를 batchmode로 돌리는 비용과 실패 지점을 없앤다 |
| D-12 | 게임 API의 대상 캐릭터는 **`X-Character-Id` 헤더**로 지정 | 메타 프롬프트 초안의 `/shop/buy` 등에 캐릭터 ID가 없다. 헤더 하나로 통일하고 서버가 소유권을 검사한다 |
| D-13 | 동시성은 **PostgreSQL 행 잠금(`SELECT ... FOR UPDATE`)** + `CHECK (gold >= 0)` | 낙관적 재시도보다 단순하고, 캐릭터 단위 직렬화로 골드 음수·아이템 중복을 막는다. 잠금 순서는 캐릭터 → 봇 → 바닥 아이템 |
| D-14 | 아이템 템플릿·몬스터·스폰·구역은 **shared 정적 카탈로그**, 확률·배율은 **appsettings `Game` 섹션** | 클라이언트와 서버가 같은 ID를 쓴다. 운영 값은 재배포 없이 설정으로 바꾼다 |
| D-15 | GM 덮어쓰기(강화 확률, 드랍 확률)는 **서버 메모리**에 둔다 | QA 테스트 격리에 유리하다(재시작하면 기본값). 영구 저장이 필요해지면 테이블로 옮긴다 |
| D-16 | 안전 구간 해석. **결과 수치 +3까지 100% 성공**(현재 +0~+2에서 시도) | "+0~+3 안전" 을 "+3까지는 안전하게 올린다" 로 해석. 설정 `SafeLevel: 3` 으로 바꿀 수 있다 |
| D-17 | 강화 주문서는 **무기용·방어구용 2종** | 장르 관례. 주문서 불일치는 `SCROLL_MISMATCH` 로 거부 |
| D-18 | 성향 상태는 **악명(성향치 < 0) > 보라(선공 타이머) > 선량** 우선순위 | 악명은 성향치로, 보라는 시간으로 판정해 두 축을 분리한다 |
| D-19 | 봇 처치 드랍은 5.4 규칙 하나로 처리하고, 5.9 사망 드랍의 "악명일수록 높다" 는 **봇 전리품 확률(기본 50%, 악명 80%)과 장착 장비 포함 여부**로 반영 | 봇 한 번 처치에 드랍 판정을 두 번 하면 규칙이 겹친다 |
| D-20 | 봇 처치 전리품이 인벤토리에 안 들어가면 **처치 위치 바닥에 떨어뜨린다** | 소실 금지(10장 예시 버그의 정상 동작). 상점 구매·줍기는 `INVENTORY_FULL` 로 거부 |
| D-21 | 경비병은 **악명 플레이어만** 공격하고, 악명 봇은 마을에 들어가지 않는다 | 봇이 경비병에게 죽는 경로를 만들면 "누구 처치인지" 를 보고할 API가 더 필요하다. 범위를 줄인다 |
| D-22 | HP·MP는 **클라이언트 런타임 상태**(접속 시 최대치), 서버는 레벨·경험치·골드·위치·성향을 저장 | 전투가 클라이언트 연출이라 HP를 서버에 저장할 근거가 약하다 |
| D-23 | 몬스터 처치 검증은 **스폰 존재 + 몬스터 종류 일치 + 거리(리쉬 + 15m) + (캐릭터, 스폰)별 리젠 쿨다운** | 무한 호출 상한이 "스폰 수 ÷ 리젠 시간" 으로 묶인다. 광역 스킬로 여러 마리를 동시에 잡아도 정상 처리된다 |
| D-24 | 마이그레이션은 **EF Core Migrations**, 서버 시작 시 자동 적용 | 스키마 이력을 코드로 남기는 상용 관례 |
| D-25 | Unity 라이선스 Secret이 없으면 Unity CI 잡은 **경고 후 건너뛴다** | 서버 CI 결과를 Unity 설정과 분리해 항상 볼 수 있게 한다 |
| D-26 | 로컬에서 **Unity 관리 DLL을 참조해 C# 컴파일만 검사하는 하네스**(`tools/unity-compile-check`)를 둔다 | CI 반복 비용이 크다. 푸시 전에 컴파일 오류를 잡는다. Editor 실행이 아니라 DLL 참조만 쓰므로 메모리 제약을 지킨다 |
| D-27 | API 초안에 없던 **`POST /characters/{id}/use-item`** 을 추가한다 | 물약 퀵슬롯·자동사냥 물약은 인벤토리를 바꾸므로 서버 권위 원칙상 서버 API가 필요하다 |
| D-28 | Unity 버전은 **6000.0.84f1** (Boss Room 원본은 6000.0.52f1) | CVE-2025-59489 가 6000.0.58f2 미만 빌드에 영향을 준다(Android 코드 실행). 같은 6000.0 LTS 라인이라 Boss Room 에셋 호환성 문제는 낮다 |
| D-29 | AltTester SDK(2.3.2, GPL-3)는 **QA 빌드 잡에서만 manifest 에 추가**한다 — D-39 로 AltTester 를 뺐다 | SDK 런타임 105개 파일 중 8개만 `#if ALTTESTER` 로 감싸져 있고 link.xml 로 보존되므로, 패키지가 있으면 Release 에도 코드가 들어간다. 패키지 자체를 빼야 "Release 는 AltTester 제외" 가 지켜진다. QA 전용 코드는 여전히 정의 심볼(`ALTTESTER`, `QA_BUILD`)로 분기한다 |
| D-30 | GameCI는 **unity-builder@v4, unity-test-runner@v4**, Unity Personal 라이선스(Secrets 3종) → **D-34 로 대체** | 공식 문서 예제가 v4 기준이고 안정적이다. unity-activate 는 필요 없다 |
| D-31 | 미니맵은 **UI 점(dot) 방식**(카메라 렌더 텍스처 아님) | 모바일에서 카메라 하나를 더 그리는 비용이 없고, AltTester로 점 오브젝트를 바로 확인할 수 있다 |
| D-32 | 우리 스크립트·에셋의 **`.meta` 파일은 커밋하지 않는다** | 로컬에 Editor 가 없어 Unity 가 만든 meta 를 받을 수 없다. 씬·프리팹이 같은 CI 실행 안에서 생성되어 GUID 가 그 실행 안에서만 일관되면 충분하다. 사람이 Editor 로 작업하게 되면 그때 meta 를 커밋한다 |
| D-33 | 봇은 몬스터에게 죽지 않는다(HP 1에서 멈춤). 봇 사망은 플레이어 처치(`bot-kill`)로만 생긴다 | 봇 사망은 서버 상태(사망·부활·전리품)라서 보고 API 가 있는 경로로만 일어나게 한다 |
| D-34 | Unity 라이선스는 **계정 로그인 방식 Personal 활성화**(Secrets `UNITY_EMAIL`, `UNITY_PASSWORD` 2종)로 하고 빌더를 **unity-builder@v6** 로 올린다. build 매트릭스는 하나씩 돌리고 진행 중인 실행은 취소하지 않는다 | Unity 가 Personal 수동 활성화를 없애 `.ulf` 경로가 막혔다. GameCI cli(2026-09, game-ci/cli#246)가 Unity Licensing Client 로 계정에서 Personal 좌석을 받는 방식을 넣었고, 이 방식은 cli 기반인 unity-builder v6 와 unity-test-runner v4.4.0 에만 있다(builder v4.8.2 는 `.ulf`·시리얼 필수). Personal 좌석이 적고 취소된 잡은 좌석을 반납하지 못할 수 있다. 2단계 인증·소셜 로그인 계정은 쓸 수 없다 |
| D-35 | 저장소 코드를 **GPL-3.0** 으로 공개한다(`LICENSE`). Boss Room 아트·CC0 에셋·폰트는 각자의 라이선스를 그대로 따른다 | 저장소가 공개되어 QA APK 아티팩트를 누구나 받을 수 있고, QA APK 에는 GPL-3.0 인 AltTester SDK 가 들어간다. AltTester 문서는 SDK 가 든 앱을 배포하면 앱에도 GPL-3.0 을 적용하라고 한다. 암호 zip 대안보다 단순해서 사용자가 이 방법을 골랐다(2026-10-21) |
| D-36 | QA 전용 **어드민 API**(`/admin/*`, `X-Admin-Key`)와 서버가 제공하는 **백오피스 웹**(`/backoffice`, 단일 HTML)을 둔다. GM API 는 그대로 둔다 | GM API 는 로그인한 본인 캐릭터만 다뤄서 Test Hub 가 밖에서 사전 조건을 만들고 결과를 검증할 수 없다. 플레이어 JWT 와 분리한 고정 키 하나로 충분하고, QA 환경 + 키 설정이 둘 다 있어야 열린다. 백오피스는 키를 담지 않고 사용자가 입력한 키로 같은 API 를 부른다 |
| D-37 | 확률 판정을 **결과 큐로 고정**(강화·봇 전리품·사망 드랍)할 수 있게 한다 | 확률 기능을 매번 같은 결과로 자동 테스트해야 한다. 난수 값을 넣는 방식은 현재 확률표에 따라 의미가 바뀌어 테스트가 깨지기 쉽다. 결과 자체를 지정하는 쪽이 읽기 쉽다. 안전 구간 강화는 규칙상 항상 성공이라 제외한다 |
| D-38 | **몬스터 스폰 표를 서버 DB**(`monster_spawns`)로 옮기고 클라이언트는 월드 입장 때 받는다. 몬스터 능력치는 shared 에 둔다 | 몬스터 수·위치를 APK 재빌드 없이 바꾸려는 요구. 처치 검증도 같은 표를 쓰므로 서버가 원본이어야 한다. 능력치까지 옮기면 클라이언트 전투 계산 전체를 서버 데이터에 맞춰야 해서 이번 범위에서 뺐다 |
| D-39 | QA 빌드의 게임 조작 SDK 를 AltTester 에서 **control_cli**(MIT)로 바꾼다. 저장소에는 넣지 않고 QA 빌드 잡이 `control-cli instrument` 로 manifest(커밋 고정)와 asmdef 참조를 더하고, `CiBuild` 가 정의 심볼 `CONTROL_CLI` 를 더한다. `qa-tests/`(AltTester Java)는 지운다 | Test Hub 가 AltTester Desktop 없이 게임을 직접 조작한다(무료판 화면 켜 두기 · 헤드리스는 Pro). 둘 다 `Input` 을 바꿔치기해 같이 넣으면 부딪친다. SDK 는 `CONTROL_CLI` 가 없으면 코드가 아예 빌드에 안 들어가 Release 에 남지 않는다. 테스트 어셈블리 포함(`IncludeTestAssemblies`)은 AltTester 때문이라 같이 뺐다. D-35 의 근거였던 AltTester 는 사라졌지만 라이선스는 그대로 둔다 |
| D-40 | QA 빌드 잡 끝에 **QA APK 를 QA Hub 에 올린다**(`POST /api/builds`, 빌드 번호 `github.run_number`). 토큰 `QAHUB_TOKEN` · 주소 `QAHUB_URL` 은 Secret, 대상 서버 `QAHUB_ENV` 는 Variable 이다. 셋 중 하나라도 없으면 경고만 남기고 건너뛴다. 같은 번호 409 는 통과로 본다 | QA Hub 가 빌드마다 스모크 → 회귀를 돌리려면 CI 가 사람 없이 알려야 한다. versionCode 가 이미 `run_number` 라 같은 번호를 보내면 QA Hub 가 폰에 깔린 빌드와 견줄 수 있다. 공개 저장소라 로그에 QA Hub 주소가 안 남게 주소도 Secret 으로 둔다. 「실패한 잡 다시 실행」은 같은 `run_number` 로 다시 올려 409 가 되므로 실패로 보지 않는다. APK 는 아티팩트 업로드 뒤에 올려 QA Hub 가 실패해도 APK 는 남는다 |
| D-41 | QA 대상 게임 서버를 **운영 서버 `/opt/ashveil` 에 `Qa` 환경으로** 올린다(`https://game.maxihan.com`, `infra/prod/`). 서버가 저장소를 git clone 해 이미지를 직접 빌드하고, `JWT_KEY` · `ADMIN_KEY` · DB 비밀번호는 서버에서 `openssl rand` 로 한 번 만든다. DB 는 새로 시작한다(맥 DB 는 옮기지 않는다) | Mac 이 무거워 상시 서비스를 클라우드로 옮기는 운영 계획(Auth_platform `docs/superpowers/plans/2026-10-27-infra-ops-plan.md`, OPS-F4-06). 공개 도메인이지만 QA 대상이라 GM · 어드민이 켜져 있어야 한다 — GM 은 로그인한 본인 캐릭터만, 어드민은 키가 있어야 해서 공개 기본키(`change-me`)만 바꾸면 된다. Mac 은 arm64 라 서버(x86_64)에서 빌드한다. 저장소가 공개라 서버에 배포 키가 필요 없다. 맥 DB 는 시험 데이터라 옮기지 않고, 계정 · 캐릭터는 가입 API 로 다시 만든다 |
