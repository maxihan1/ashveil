# CI 설계와 설정 방법

## 워크플로 한눈에 보기

| 워크플로 | 언제 | 하는 일 | 라이선스 필요 |
|---|---|---|---|
| `server-ci` | PR, main 푸시 (server·shared 변경) | shared 단위 테스트 + 실제 PostgreSQL 통합 테스트, 서버 Docker 이미지 빌드 | 아니오 |
| `unity-ci` / compile-check | PR, main 푸시 (client·shared·tools 변경) | Unity 6000.0.84f1 관리 DLL로 클라이언트 C# 컴파일 검사(Release·QA·Editor 3가지 정의 조합), EditMode 테스트 실행 | 아니오 |
| `unity-ci` / test | 위와 같음 | GameCI `unity-test-runner@v4` 로 EditMode + PlayMode 테스트 | 예 |
| `unity-ci` / build | main 푸시, 수동 실행 | Boss Room 아트 가져오기 → 씬·프리팹 생성 → QA APK, Release APK 빌드 → 아티팩트 업로드 | 예 |

Unity 라이선스 Secret이 없으면 test·build 잡은 건너뛰고 경고만 남긴다(D-25). compile-check 와 server-ci 는 항상 돈다.

## 1. 개발자가 할 일: Unity Personal 라이선스 Secrets 등록 (10분)

Unity 는 CI 안에서도 활성화된 라이선스가 있어야 실행된다. 무료 Personal 라이선스면 된다(Unity 약관상 최근 12개월 매출·투자 20만 달러 미만). Unity 가 Personal 의 수동 활성화(`.ulf` 파일)를 없앴기 때문에, GameCI 가 CI 안에서 Unity 계정 이메일·비밀번호로 Personal 좌석을 직접 받는다(D-34).

1. CI 전용 Unity 계정을 만든다(https://id.unity.com). 구글·애플 소셜 로그인이 아닌 이메일·비밀번호 계정이어야 하고, **2단계 인증은 끈다**.
2. Mac 에 **Unity Hub만** 설치하고 그 계정으로 로그인한 뒤 설정(Preferences) → Licenses → **Add** → **Get a free personal license** 를 한 번 누른다. 계정에 Personal 라이선스가 생긴다. Editor 는 설치하지 않는다.
3. GitHub 저장소 → Settings → Secrets and variables → Actions → **New repository secret** 으로 2개를 만든다.

| Secret | 값 |
|---|---|
| `UNITY_EMAIL` | 1번 계정 이메일 |
| `UNITY_PASSWORD` | 1번 계정 비밀번호 (특수문자가 없으면 문제가 적다) |

4. Actions 탭 → `unity-ci` → **Run workflow** 로 수동 실행하면 테스트와 APK 빌드가 돈다.

- `UNITY_LICENSE`(`.ulf`) Secret 은 만들지 않는다. Hub 가 만든 `.ulf` 는 그 Mac 에 묶여 있어 CI 러너에서는 실패한다.
- Personal 좌석은 적다. 그래서 build 잡은 QA·Release 를 차례로 빌드하고, 새 푸시가 와도 돌고 있는 실행을 취소하지 않는다(취소된 잡은 좌석을 반납하지 못할 수 있다).
- 활성화가 좌석 부족(no available Personal seats)으로 실패하면 https://id.unity.com 에서 남아 있는 활성화를 해제하고 다시 실행한다. Unity 가 새 기기 확인 메일을 보냈다면 승인한 뒤 다시 실행한다.

### 선택 Secrets·Variables

| 종류 | 이름 | 용도 |
|---|---|---|
| Secret | `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS` | Release 서명 키. 없으면 디버그 서명으로 빌드한다 |
| Secret | `ANDROID_QA_KEYSTORE_BASE64`, `ANDROID_QA_KEYSTORE_PASS`, `ANDROID_QA_KEYALIAS_NAME`, `ANDROID_QA_KEYALIAS_PASS` | QA 서명 키(등록됨, 2026-10-21). 빌드마다 같은 키라 기존 앱을 지우지 않고 덮어 설치된다. 원본은 만든 Mac 의 `~/.config/ashveil/qa-signing/` 에 있다 |
| Variable | `GAME_SERVER_URL` | APK 에 넣을 기본 서버 주소 (예: `http://192.168.0.10:8080`). 앱에서 바꿀 수도 있다 |

## 2. build 잡 흐름

```
free-disk-space → checkout
→ tools/fetch-bossroom.sh       Boss Room 고정 커밋 1299ba4f 에서 아트 폴더만 sparse + LFS 로 받기 (캐시)
→ (QA만) control-cli instrument client   manifest 에 control_cli SDK(CONTROL_CLI_REF 커밋) + asmdef 에 ControlCli.Input 참조
→ game-ci/unity-builder@v6       -executeMethod Ashveil.Editor.CiBuild.BuildAndroid
     ├ ContentGenerator.GenerateAll()   URP 에셋, 프리팹, Boot·World 씬 생성
     ├ 변형별 설정                       번들 ID, 앱 이름, 정의 심볼(QA_BUILD · CONTROL_CLI), HTTP 허용, 서버 주소
     └ BuildPipeline.BuildPlayer         IL2CPP ARM64 APK (QA 는 Development 빌드)
→ 아티팩트 Ashveil-qa-apk, Ashveil-release-apk
```

| | QA 빌드 | Release 빌드 |
|---|---|---|
| 번들 ID | `com.nimbusgames.ashveil.qa` | `com.nimbusgames.ashveil` |
| 정의 심볼 | `QA_BUILD`, `CONTROL_CLI` | 없음 |
| control_cli SDK | 포함 (CI가 패키지 추가, D-39) | 패키지 자체가 없음 |
| GM 콘솔 | 있음 | 코드가 컴파일되지 않음 |
| HTTP 평문 | 허용 (`usesCleartextTraffic=true`) | 차단 (HTTPS 서버 필요) |
| 빌드 옵션 | Development | 일반 |

## 3. 빌드 인자 (`-executeMethod Ashveil.Editor.CiBuild.BuildAndroid`)

| 인자 | 기본값 | 설명 |
|---|---|---|
| `-buildVariant` | `qa` | `qa` 또는 `release` |
| `-buildNumber` | `1` | versionCode, 버전 `0.1.<n>` |
| `-serverUrl` | 없음 | `Resources/server-config.json` 에 들어갈 기본 서버 주소 |
| `-customBuildPath` | GameCI가 넘김 | 출력 APK 경로 |

## 4. 로컬에서 할 수 있는 검증 (Unity 설치 없이)

```bash
# 서버 + shared 테스트 (Docker)
docker network create ashveil-test; docker run -d --name ashveil-test-db --network ashveil-test -e POSTGRES_PASSWORD=postgres postgres:16-alpine
DOCKER_NETWORK=ashveil-test tools/dotnet-docker.sh test server/Ashveil.slnx

# Unity C# 컴파일 검사 + EditMode 테스트 (최초 1회 Unity 배포본 4.5GB 스트리밍 추출)
UNITY_REF_DIR=$HOME/.cache/unity-refs/6000.0.84f1 tools/unity-compile-check/fetch-unity-refs.sh
UNITY_REF_DIR=$HOME/.cache/unity-refs/6000.0.84f1 python3 tools/unity-compile-check/check.py
UNITY_REF_DIR=$HOME/.cache/unity-refs/6000.0.84f1 python3 tools/unity-compile-check/check.py editmode-run

# Boss Room 아트 받기 (git-lfs 없이 Docker 로)
tools/fetch-bossroom-docker.sh
```
