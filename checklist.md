# 작업 체크리스트

진행 상황을 단계별로 체크한다. 세부 결정은 `context-notes.md`, 설계 근거는 `docs/decisions.md`.

## 0단계 설계
- [x] 아키텍처·도메인 모델·ERD·씬 생성 전략 (`docs/architecture.md`)
- [x] 규칙·확률표 (`docs/game-rules.md`)
- [x] API 명세 확정안 (`docs/api.md`)
- [x] 결정 기록 (`docs/decisions.md`)
- [ ] CI 설계·Secrets 안내 (`docs/ci.md`)

## 1단계 기반
- [ ] shared 도메인 + xUnit 테스트
- [ ] 서버 골격, Docker Compose, 인증 API + 통합 테스트
- [ ] Unity 프로젝트 골격, manifest, ProjectSettings
- [ ] Boss Room fetch 스크립트, 생성기(Boot/World 씬), 빌드 스크립트
- [ ] 로그인 화면 + 서버 로그인
- [ ] GameCI 파이프라인 (test, QA/Release APK)
- [ ] 로컬 Unity 컴파일 체크 하네스

## 2단계 월드·플레이어
- [ ] 필드 맵(구역 4개), 플레이어 이동·카메라, 미니맵, 구역 토스트, 귀환 버튼
- [ ] 캐릭터 생성·선택, 위치 저장·복원

## 3단계 전투·재화
- [ ] 몬스터 AI·스폰, 전투, 사망·부활, 처치 골드·경험치, 원장, 자동사냥

## 4단계 인벤토리·상점·강화
- [ ] 도메인 테스트 → 서버 API → 클라이언트 UI

## 5단계 봇
- [ ] 봇 시드·API, 봇 AI(3성향), 봇 처치 드랍

## 5-1단계 PK
- [ ] PK 모드, 성향치, 사망 드랍, 바닥 아이템·줍기(봇 포함), 경비병, PK 기록

## 6단계 QA 마감
- [ ] AltTester SDK 통합, GM 콘솔, 빌드 인자
- [ ] 실행 가이드, 단계별 폰 체크리스트, JUnit5 샘플
