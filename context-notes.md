# 컨텍스트 노트

작업 중 내린 결정과 이유를 시간순으로 붙인다. 큰 설계 결정은 `docs/decisions.md` 에 ID를 붙여 옮긴다.

## 2026-10-21
- 작업 환경. 로컬에 dotnet 없음, Docker(4.8GB) 있음 → 서버 빌드·테스트는 `mcr.microsoft.com/dotnet/sdk:10.0` 컨테이너에서 돌린다.
- GitHub 비공개 저장소 `maxihan1/ashveil` 생성. 작업은 `worktree-game-dev` 브랜치, main 직접 푸시 금지 → PR로 합친다.
- Boss Room 원본 커밋 `1299ba4fc97d83b634e7d32a86c438ba668d7f9c`(2025-10-23), Unity `6000.0.52f1`. 이 버전에 맞춘다.
- Boss Room 캐릭터 애니메이터 `CharacterSetController` 파라미터. `Speed`(float), `Attack1`, `Attack2`, `HitReact1`, `FallDown`, `Dead`, `SkillHeal`, `Buff1` 등(trigger). 애니메이션 이벤트는 `OnAnimEvent` 하나 → 수신용 빈 메서드 컴포넌트가 필요하다.
- Boss Room 그래픽 프리팹(`Prefabs/CharGFX/CharacterGraphics/PlayerGraphics_*`, `ImpGraphics`, `VandalImpGraphics`, `BossGraphics`)에는 Boss Room 스크립트(ClientCharacter 등)가 붙어 있다. 스크립트를 복사하지 않으므로 "Missing Script" 로 남는다. 동작에는 영향이 없고, 생성기가 프리팹을 만들 때 `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` 로 정리한다.
- 스폰 좌표는 결정적 해시로 한 번 생성해 `SpawnTable` 에 리터럴로 넣었다(언어별 Random 구현 차이 회피).
