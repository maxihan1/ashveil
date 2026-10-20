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
| D-29 | AltTester SDK(2.3.2, GPL-3)는 **QA 빌드 잡에서만 manifest 에 추가**한다 | SDK 런타임 105개 파일 중 8개만 `#if ALTTESTER` 로 감싸져 있고 link.xml 로 보존되므로, 패키지가 있으면 Release 에도 코드가 들어간다. 패키지 자체를 빼야 "Release 는 AltTester 제외" 가 지켜진다. QA 전용 코드는 여전히 정의 심볼(`ALTTESTER`, `QA_BUILD`)로 분기한다 |
| D-30 | GameCI는 **unity-builder@v4, unity-test-runner@v4**, Unity Personal 라이선스(Secrets 3종) | 공식 문서 예제가 v4 기준이고 안정적이다. unity-activate 는 필요 없다 |
| D-31 | 미니맵은 **UI 점(dot) 방식**(카메라 렌더 텍스처 아님) | 모바일에서 카메라 하나를 더 그리는 비용이 없고, AltTester로 점 오브젝트를 바로 확인할 수 있다 |
