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

1. Mac 에 **Unity Hub만** 설치한다. Editor 는 설치하지 않아도 된다.
2. Unity Hub → 설정(Preferences) → Licenses → **Add** → **Get a free personal license** 를 누른다. 이미 라이선스가 보여도 Add 를 누른다.
3. 터미널에서 라이선스 파일을 클립보드로 복사한다.
   ```bash
   cat "/Library/Application Support/Unity/Unity_lic.ulf" | pbcopy
   ```
4. GitHub 저장소 → Settings → Secrets and variables → Actions → **New repository secret** 으로 3개를 만든다.

| Secret | 값 |
|---|---|
| `UNITY_LICENSE` | 3번에서 복사한 `.ulf` 파일 내용 전체 |
| `UNITY_EMAIL` | Unity 계정 이메일 |
| `UNITY_PASSWORD` | Unity 계정 비밀번호 (특수문자가 없으면 문제가 적다) |

5. Actions 탭 → `unity-ci` → **Run workflow** 로 수동 실행하면 테스트와 APK 빌드가 돈다.

### 선택 Secrets·Variables

| 종류 | 이름 | 용도 |
|---|---|---|
| Secret | `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASS`, `ANDROID_KEYALIAS_NAME`, `ANDROID_KEYALIAS_PASS` | Release 서명 키. 없으면 디버그 서명으로 빌드한다 |
| Variable | `GAME_SERVER_URL` | APK 에 넣을 기본 서버 주소 (예: `http://192.168.0.10:8080`). 앱에서 바꿀 수도 있다 |
| Variable | `ALTTESTER_HOST`, `ALTTESTER_PORT`, `ALTTESTER_APP_NAME` | QA 빌드의 AltTester 서버 주소·포트·앱 이름 (기본 `127.0.0.1`, `13000`, `ashveil`) |

## 2. build 잡 흐름

```
free-disk-space → checkout
→ tools/fetch-bossroom.sh       Boss Room 고정 커밋 1299ba4f 에서 아트 폴더만 sparse + LFS 로 받기 (캐시)
→ (QA만) tools/ci/enable-alttester.py   manifest 에 com.alttester.sdk 2.3.2 (OpenUPM) 추가
→ game-ci/unity-builder@v4       -executeMethod Ashveil.Editor.CiBuild.BuildAndroid
     ├ ContentGenerator.GenerateAll()   URP 에셋, 프리팹, Boot·World 씬 생성
     ├ 변형별 설정                       번들 ID, 앱 이름, 정의 심볼(QA_BUILD), HTTP 허용, 서버 주소
     ├ (QA만) AltTester 계측             ALTTESTER 정의 + 첫 씬에 AltTester 프리팹(호스트·포트·앱 이름)
     └ BuildPipeline.BuildPlayer         IL2CPP ARM64 APK (QA 는 Development 빌드)
→ 아티팩트 Ashveil-qa-apk, Ashveil-release-apk
```

| | QA 빌드 | Release 빌드 |
|---|---|---|
| 번들 ID | `com.nimbusgames.ashveil.qa` | `com.nimbusgames.ashveil` |
| 정의 심볼 | `QA_BUILD`, `ALTTESTER` | 없음 |
| AltTester SDK | 포함 (CI가 패키지 추가) | 패키지 자체가 없음 |
| GM 콘솔 | 있음 | 코드가 컴파일되지 않음 |
| HTTP 평문 | 허용 (`usesCleartextTraffic=true`) | 차단 (HTTPS 서버 필요) |
| 빌드 옵션 | Development | 일반 |

## 3. 빌드 인자 (`-executeMethod Ashveil.Editor.CiBuild.BuildAndroid`)

| 인자 | 기본값 | 설명 |
|---|---|---|
| `-buildVariant` | `qa` | `qa` 또는 `release` |
| `-buildNumber` | `1` | versionCode, 버전 `0.1.<n>` |
| `-serverUrl` | 없음 | `Resources/server-config.json` 에 들어갈 기본 서버 주소 |
| `-altHost` / `-altPort` / `-altAppName` | `127.0.0.1` / `13000` / `ashveil` | AltTester 서버 접속 정보 |
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
