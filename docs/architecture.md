# 아키텍처 설계 (0단계)

게임 이름은 **Ashveil(애쉬베일)**, 개발사 이름은 가상의 **Nimbus Games**다. 리니지(NCSoft)의 이름·고유명사·리소스는 쓰지 않는다.

## 1. 전체 구조

```
[Android 폰: Unity 클라이언트] --REST/JSON--> [ASP.NET Core 서버] --EF Core--> [PostgreSQL]
        │  (QA 빌드만) AltTester SDK  ──WebSocket──> [AltTester Desktop / Test Hub]
        └─ shared 패키지(순수 C#)  <── 같은 소스 ──>  server 가 프로젝트 참조
```

- **서버 권위(authoritative)**. 재화, 드랍, 강화, 인벤토리, 성향치는 서버만 바꾼다. 클라이언트는 응답 값을 표시만 한다.
- **전투 연출과 AI는 클라이언트**. 데미지 계산, 몬스터·봇 이동은 클라이언트가 하고, 결과(처치·사망·선공)를 서버 API로 보고한다. 서버는 쿨다운·거리·존재 여부로 최소 검증한다.
- **shared**는 Unity 의존성이 없는 순수 C#(netstandard2.1, C# 9) 라이브러리다. 서버는 프로젝트 참조로, Unity는 로컬 UPM 패키지(`file:../../shared/Package`)로 같은 소스를 쓴다.

## 2. 저장소 구조

```
/client                         Unity 6 프로젝트 (6000.0.x LTS, URP, Android IL2CPP ARM64)
  Assets/Game/Domain            클라이언트 전용 순수 C# (asmdef: noEngineReferences)
  Assets/Game/Application       API 클라이언트, 세션, 유스케이스 서비스
  Assets/Game/Presentation      MonoBehaviour, 런타임 UI, 연출, AI 구동
  Assets/Game/Editor            씬·프리팹 생성, 빌드 스크립트(CI 진입점)
  Assets/Game/Generated         CI가 생성하는 씬·프리팹 (gitignore)
  Assets/ThirdParty/BossRoom    CI에서 tools/fetch-bossroom.sh 로 채움 (gitignore)
  Assets/ThirdParty/CC0         Kenney 등 CC0 에셋 (저장소에 포함)
  Assets/Tests/EditMode, PlayMode
/shared                         공유 도메인 규칙 + DTO
  Package/                      Unity 패키지 루트 (package.json, asmdef, Runtime/*.cs)
  Ashveil.Shared.csproj         같은 소스를 netstandard2.1 로 빌드
/server
  src/Ashveil.Server            Minimal API
  tests/Ashveil.Shared.Tests    도메인 단위 테스트 (xUnit)
  tests/Ashveil.Server.Tests    실제 PostgreSQL 통합 테스트
/infra                          docker-compose.yml, .env.example
/qa-tests                       AltTester Java(JUnit5) 스모크 테스트 샘플
/tools                          fetch-bossroom.sh, Unity 컴파일 체크 하네스
/.github/workflows              server-ci.yml, unity-ci.yml
/docs                           설계, API, 규칙표, 실행 가이드, 단계별 폰 체크리스트
/ThirdPartyNotices              Unity Companion License, CC0/OFL 출처
```

Unity는 `Assets/` 밖의 폴더를 컴파일하지 않으므로, 메타 프롬프트의 `Tests/EditMode, Tests/PlayMode` 는 `client/Assets/Tests/` 아래에 둔다(decisions.md D-07).

## 3. 도메인 모델

### 3.1 바운디드 컨텍스트

| 컨텍스트 | 핵심 개념 | 규칙 위치 |
|---|---|---|
| 계정 | Account, RefreshToken | server |
| 캐릭터 | Character, ClassType, 스탯, 레벨/경험치 | shared `StatRules`, `ExpTable` |
| 인벤토리·장비 | ItemTemplate(정적 카탈로그), ItemInstance, EquipSlot | shared `ItemCatalog`, `InventoryRules` |
| 재화 | Gold, LedgerEntry | server (트랜잭션 + 원장) |
| 상점 | 판매 목록, 가격 배율 | shared `ShopRules` |
| 강화 | EnhanceRateTable, EnhanceResult | shared `EnhanceRules` |
| 월드 | ZoneId, ZoneMap, SpawnPoint, MonsterType | shared `ZoneMap`, `SpawnTable`, `MonsterCatalog` |
| 봇 | Bot, Temperament | shared `BotRules`, server 시드 |
| PK·성향 | Alignment, AlignmentState, PurpleUntil, PkRecord, GroundItem | shared `AlignmentRules`, `DropRules` |

### 3.2 핵심 불변식

1. 골드는 음수가 될 수 없다(DB `CHECK (gold >= 0)` + 행 잠금).
2. 하나의 아이템 인스턴스는 정확히 한 소유자(캐릭터·봇·바닥)에 속한다. 이전은 소유자 컬럼 갱신 한 번으로 끝나므로 중복이 생기지 않는다.
3. 캐릭터가 소유한 인스턴스 수(스택 1개 = 1칸, 장착 중 포함)는 40을 넘지 않는다.
4. 모든 골드 변동은 같은 트랜잭션 안에서 원장 행을 남긴다(`BalanceAfter` = 변경 후 잔액).
5. 강화·드랍·골드량 판정은 서버 RNG만 쓴다.

### 3.3 동시성 전략

- 상태를 바꾸는 요청은 `BEGIN` → `SELECT ... FOR UPDATE`(캐릭터 → 봇 → 바닥 아이템 순서로 잠가 교착을 막음) → 검증 → 변경 → `COMMIT` 으로 처리한다.
- 같은 캐릭터에 대한 동시 요청은 캐릭터 행 잠금으로 직렬화된다. 같은 바닥 아이템을 동시에 주우면 하나만 성공한다.

## 4. ERD

```mermaid
erDiagram
    accounts ||--o{ refresh_tokens : has
    accounts ||--o{ characters : owns
    characters ||--o{ ledger_entries : "gold changes"
    characters ||--o{ enhance_histories : ""
    characters ||--o{ spawn_cooldowns : ""
    items }o--|| characters : "owner_kind=Character"
    items }o--|| bots : "owner_kind=Bot"
    items ||--o| ground_items : "owner_kind=Ground"
    pk_records }o--|| characters : "killer/victim"
    alignment_histories }o--|| characters : ""

    accounts { bigint id PK; text username UK; text password_hash; timestamptz created_at }
    refresh_tokens { bigint id PK; bigint account_id FK; text token_hash UK; timestamptz expires_at; timestamptz revoked_at }
    characters { bigint id PK; bigint account_id FK; text name "UK lower(name) where deleted_at is null"; text class_type; text gender; int level; bigint exp; bigint gold "CHECK >= 0"; real pos_x; real pos_y; real pos_z; int alignment; timestamptz purple_until; int pk_kills; int pk_deaths; timestamptz created_at; timestamptz deleted_at }
    bots { bigint id PK; text name UK; text class_type; text gender; int level; text zone; text temperament; int alignment; timestamptz purple_until; bool is_dead; timestamptz respawn_at; int kills; int deaths }
    items { bigint id PK; text template_id; int quantity; int enhance_level; text owner_kind; bigint owner_id; text equipped_slot; timestamptz created_at }
    ground_items { bigint item_id PK,FK; text zone; real x; real z; timestamptz dropped_at; timestamptz expires_at; text dropped_by_kind; bigint dropped_by_id }
    ledger_entries { bigint id PK; bigint character_id FK; text reason; bigint delta; bigint balance_after; text related_type; text related_id; timestamptz created_at }
    enhance_histories { bigint id PK; bigint character_id FK; bigint item_id; text template_id; int from_level; int to_level; text result; bigint gold_cost; timestamptz created_at }
    spawn_cooldowns { bigint character_id PK; text spawn_id PK; timestamptz last_killed_at }
    pk_records { bigint id PK; text killer_kind; bigint killer_id; text victim_kind; bigint victim_id; bool victim_was_innocent; int alignment_delta; timestamptz created_at }
    alignment_histories { bigint id PK; text entity_kind; bigint entity_id; int delta; int after; text reason; timestamptz created_at }
```

아이템 템플릿, 몬스터, 스폰, 구역은 DB가 아니라 shared 코드의 정적 카탈로그다. 클라이언트와 서버가 같은 표를 보므로 ID가 어긋날 수 없다.

## 5. 씬·프리팹 생성 전략

Unity Editor를 로컬에서 띄우지 않으므로 **씬·프리팹은 전부 에디터 스크립트가 CI에서 생성**하고, 저장소에는 생성 코드만 둔다.

1. CI가 `tools/fetch-bossroom.sh` 로 Boss Room 저장소(고정 커밋, Git LFS)를 받아 아트 폴더만 `Assets/ThirdParty/BossRoom` 에 복사한다. `.meta` 를 같이 복사하므로 GUID 참조(머티리얼↔텍스처↔FBX)가 유지된다.
2. 빌드 진입점 `Ashveil.Editor.CiBuild.BuildAndroid` 가 빌드 전에 `ContentGenerator.GenerateAll()` 을 호출해 다음을 만든다.
   - `Assets/Game/Generated/Resources/Prefabs` 캐릭터(전사·궁수·마법사), 몬스터(임프·반달 임프·보스), NPC(상인·강화사·경비병) 프리팹. Boss Room 그래픽 프리팹을 중첩하고, 없으면 캡슐로 대체한다.
   - `Assets/Game/Generated/Scenes/Boot.unity` 로그인·캐릭터 선택.
   - `Assets/Game/Generated/Scenes/World.unity` 400×400m Terrain(절차적 높이맵·텍스처), CC0 나무·바위, 마을 프랍, 보스 지역 던전 프랍, 조명.
   - URP 파이프라인 에셋 지정(Boss Room `URP/Mobile` 설정 재사용), Build Settings 씬 목록, Android 플레이어 설정.
3. 몬스터 스폰·봇·NPC 배치는 shared 데이터(`SpawnTable`, `ZoneMap`, `WorldLayout`)를 런타임에 읽어 생성한다. 서버 검증 좌표와 클라이언트 배치가 항상 같다.
4. UI는 런타임에 코드로 생성한다(`UiFactory`). 프리팹 YAML 직렬화 실수를 원천 차단하고, AltTester로 볼 때 오브젝트 이름이 코드와 1:1로 대응한다.

## 6. 클라이언트 계층

| 계층 | asmdef | 참조 | 내용 |
|---|---|---|---|
| Shared | `Ashveil.Shared` | 없음 | 규칙, 카탈로그, DTO |
| Domain | `Ashveil.Domain` | Shared | 몬스터 AI 상태기계, 봇 의사결정, 자동사냥 정책, 에러 문구 매핑 |
| Application | `Ashveil.Application` | Shared, Domain, Newtonsoft | `ApiClient`(UnityWebRequest), `Session`, 서비스 |
| Presentation | `Ashveil.Presentation` | 위 전부 | 부트스트랩, 플레이어·몬스터·봇 컨트롤러, UI, GM 콘솔(`QA_BUILD`) |
| Editor | `Ashveil.Editor` | 위 전부 | 생성기, 빌드, AltTester 계측(`ALTTESTER`) |

## 7. CI 설계 요약

자세한 내용과 Secrets는 [ci.md](ci.md).

- `server-ci.yml` PR·main. PostgreSQL 서비스 컨테이너 + `dotnet test`(단위 + 통합).
- `unity-ci.yml` PR·main. Boss Room 가져오기 → EditMode/PlayMode 테스트(GameCI unity-test-runner) → main 머지 시 QA APK, Release APK 빌드(GameCI unity-builder) → 아티팩트 업로드.
- Unity 라이선스 Secret이 없으면 Unity 잡은 경고를 남기고 건너뛴다(서버 CI는 독립적으로 동작).
