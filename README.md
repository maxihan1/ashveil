# Ashveil (애쉬베일)

Unity Boss Room 샘플의 **아트만** 가져와 만든 리니지 라이크 모바일(Android) 게임과 그 백엔드다.
QA 자동화 플랫폼(Test Hub)이 테스트할 "고객사 게임" 역할을 한다. 개발사는 가상의 **Nimbus Games**.

- 하나의 심리스 필드(마을 · 초록 들판 · 잿빛 숲 · 불꽃 군주의 폐허), 조이스틱 이동, 탭 타겟팅, 자동사냥
- 서버 권위 재화·인벤토리·상점·강화(성공·실패·파괴)·봇 전리품·PK 성향치(선량·보라·악명)·사망 드랍·바닥 아이템
- QA 빌드에만 게임 조작 SDK([control_cli](https://github.com/maxihan1/control_cli))와 GM 콘솔

## 구성

| 폴더 | 내용 |
|---|---|
| `client/` | Unity **6000.0.84f1** (URP, Android IL2CPP ARM64). 씬·프리팹은 CI 에서 에디터 스크립트가 생성 |
| `shared/` | 클라이언트·서버 공용 순수 C# 규칙과 DTO (netstandard2.1, C# 9) |
| `server/` | ASP.NET Core 10 Minimal API + EF Core + PostgreSQL, JWT |
| `infra/` | Docker Compose (서버 + PostgreSQL) |
| `tools/` | Boss Room 가져오기, Unity 없이 C# 컴파일·EditMode 테스트 하네스, Docker dotnet 래퍼 |
| `docs/` | [설계](docs/architecture.md) · [API](docs/api.md) · [규칙·확률표](docs/game-rules.md) · [결정 기록](docs/decisions.md) · [CI](docs/ci.md) · [실행 가이드](docs/run-guide.md) · [폰 체크리스트](docs/phone-checklists.md) |

## 빠른 시작

1. **서버** (Mac, Docker Desktop)
   ```bash
   cd infra && cp .env.example .env && docker compose up -d --build
   open http://localhost:8080/swagger
   ```
2. **Unity 라이선스 Secrets 등록** (최초 1회) → [docs/ci.md](docs/ci.md#1-개발자가-할-일-unity-personal-라이선스-secrets-등록-10분)
3. **APK** GitHub Actions `unity-ci` 실행 → Artifacts `Ashveil-qa-apk` 내려받아 `adb install -r Ashveil-qa.apk`
4. **폰에서 서버 주소** 로그인 화면 → 서버 설정 → `http://<Mac LAN IP>:8080` → [docs/run-guide.md](docs/run-guide.md)
5. **게임 조작** `npm install -g github:maxihan1/control_cli` → 폰을 USB 로 잇고 `control-cli tree` → [docs/run-guide.md](docs/run-guide.md#4-게임-조작-sdk-control_cli-qa-빌드)

## 버전

| 구성 요소 | 버전 |
|---|---|
| Unity Editor | 6000.0.84f1 (GameCI 이미지 `unityci/editor:ubuntu-6000.0.84f1-android-3`) |
| URP / uGUI / Test Framework | 17.0.4 / 2.0.0 / 1.6.0 |
| **control_cli SDK** | `unity-ci.yml` 의 `CONTROL_CLI_REF` 커밋 (QA 빌드에만, D-39) |
| .NET (서버) | 10.0 |
| PostgreSQL | 16 |
| Boss Room 원본 | `Unity-Technologies/com.unity.multiplayer.samples.coop` 커밋 `1299ba4f` |

## 테스트

| 종류 | 위치 | 실행 |
|---|---|---|
| 도메인 규칙 단위 테스트 66개 | `server/tests/Ashveil.Shared.Tests` | `server-ci` |
| 서버 통합 테스트 37개 (실제 PostgreSQL, 동시성·클라이언트 직렬화 계약 포함) | `server/tests/Ashveil.Server.Tests` | `server-ci` |
| Unity EditMode 27개 | `client/Assets/Tests/EditMode` | `unity-ci` compile-check(엔진 없이 실행) + GameCI |
| Unity PlayMode | `client/Assets/Tests/PlayMode` | GameCI |

이 저장소의 코드는 [GPL-3.0](LICENSE)이다(D-35). Boss Room 아트, CC0 에셋, 나눔고딕 등 외부 에셋은 각자의 라이선스를 따른다. 라이선스·출처는 [ThirdPartyNotices](ThirdPartyNotices/README.md). 리니지(NCSoft)의 이름·고유명사·리소스는 쓰지 않는다.
