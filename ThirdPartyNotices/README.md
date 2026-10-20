# Third-Party Notices

Ashveil 클라이언트·서버가 포함하거나 빌드할 때 내려받는 외부 에셋과 라이브러리 목록이다.

## 아트·폰트

| 에셋 | 저장 위치 | 저작자 | 라이선스 | 출처 |
|---|---|---|---|---|
| Boss Room: Small Scale Co-op Sample (아트만: 캐릭터·몬스터 모델, 애니메이션, 머티리얼, 셰이더, 텍스처, 던전 프랍, VFX) | CI 빌드 때 `client/Assets/ThirdParty/BossRoom/` 로 복사 (`tools/fetch-bossroom.sh`, 커밋 `1299ba4f`) | Unity Technologies | Unity Companion License ([BossRoom-LICENSE.md](BossRoom-LICENSE.md)) | https://github.com/Unity-Technologies/com.unity.multiplayer.samples.coop |
| Nature Kit 2.1 (나무·바위·풀·텐트·울타리 FBX 38종) | `client/Assets/ThirdParty/CC0/KenneyNatureKit/` | Kenney (www.kenney.nl) | CC0 1.0 ([KenneyNatureKit-License.txt](KenneyNatureKit-License.txt)) | https://kenney.nl/assets/nature-kit |
| 496 pixel art icons for medieval/fantasy RPG (아이템 아이콘 22종, 파일명을 아이템 ID로 바꿈) | `client/Assets/Game/Resources/Icons/` | Henrique Lazarini (7Soul1), 묶음 업로드 gnola14 | CC0 1.0 (OpenGameArt 표기, 저작자의 Public Domain 표명) | https://opengameart.org/content/496-pixel-art-icons-for-medievalfantasy-rpg |
| Grass001, Ground048, PavingStones070 (Color 맵만, 1K JPG 재인코딩) | `client/Assets/ThirdParty/CC0/AmbientCG/` | ambientCG | CC0 1.0 | https://ambientcg.com |
| 나눔고딕 Regular·Bold | `client/Assets/Game/Resources/Fonts/` | NHN Corporation | SIL Open Font License 1.1 ([NanumGothic-OFL.txt](NanumGothic-OFL.txt)) | https://github.com/google/fonts/tree/main/ofl/nanumgothic |

Boss Room 에서는 아트만 쓰고 Netcode·로비·UGS 코드와 패키지는 가져오지 않는다. 리니지(NCSoft)의 이름·고유명사·리소스는 쓰지 않는다.

## 라이브러리

| 라이브러리 | 쓰는 곳 | 라이선스 |
|---|---|---|
| AltTester Unity SDK 2.3.2 (`com.alttester.sdk`) | **QA 빌드에만** CI가 추가한다. Release 빌드에는 들어가지 않는다 | GPL-3.0 |
| Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`) | 클라이언트 JSON 직렬화 | MIT |
| ASP.NET Core, EF Core, Npgsql, EFCore.NamingConventions, Swashbuckle.AspNetCore.SwaggerUI | 서버 | MIT / Apache-2.0 / PostgreSQL License |
| xUnit | 서버 테스트 | Apache-2.0 |
