# 백엔드 API 명세 (확정안)

- 기본 주소 `http://<서버 LAN IP>:8080`. 서버가 뜨면 `/swagger` 에서 OpenAPI 문서를 볼 수 있다.
- JSON 필드는 camelCase, enum은 문자열(`"Warrior"`).
- 인증이 필요한 API는 `Authorization: Bearer <accessToken>` 헤더를 쓴다.
- 경로에 캐릭터 ID가 없는 게임 API(상점, 강화, 전투, 월드, 원장, GM)는 **`X-Character-Id` 헤더**로 대상 캐릭터를 지정한다. 서버는 그 캐릭터가 토큰의 계정 소유인지 확인한다(`FORBIDDEN`).
- DTO 클래스는 `shared/Package/Runtime/Dto/` 에 있고 클라이언트와 서버가 같이 쓴다.

## 에러 형식

```json
{ "code": "INSUFFICIENT_GOLD", "message": "골드가 부족합니다." }
```

| HTTP | code | 상황 |
|---|---|---|
| 400 | `VALIDATION_FAILED` | 필드 형식 오류 |
| 401 | `UNAUTHORIZED` | 토큰 없음·만료 |
| 401 | `INVALID_CREDENTIALS` | 아이디·비밀번호 불일치 |
| 401 | `INVALID_REFRESH_TOKEN` | 리프레시 토큰 무효·만료·재사용 |
| 403 | `FORBIDDEN` | 남의 캐릭터 |
| 404 | `NOT_FOUND` | 캐릭터·아이템·봇·바닥 아이템 없음 |
| 409 | `USERNAME_TAKEN`, `NAME_TAKEN`, `CHARACTER_LIMIT` | 계정·캐릭터 생성 |
| 409 | `INSUFFICIENT_GOLD`, `INVENTORY_FULL`, `NOT_ENOUGH_QUANTITY`, `ITEM_EQUIPPED` | 상점·인벤토리 |
| 409 | `NOT_EQUIPPABLE`, `NOT_USABLE`, `SCROLL_MISMATCH`, `MAX_ENHANCE`, `NOT_SELLABLE` | 장비·소모품·강화 |
| 409 | `INVALID_SPAWN`, `OUT_OF_RANGE`, `COOLDOWN`, `SAFE_ZONE`, `BOT_DEAD`, `INVALID_TARGET`, `ITEM_EXPIRED` | 전투·월드 |
| 404 | `GM_DISABLED` | QA가 아닌 환경에서 GM API 호출 |

클라이언트는 `code` 로 한국어 안내 문구를 고른다(`Ashveil.Domain.ErrorMessages`).

## 인증

| 메서드 | 경로 | 요청 | 응답 |
|---|---|---|---|
| POST | `/auth/signup` | `{ username, password }` 아이디 4~16자 `[a-z0-9_]`, 비밀번호 6~64자 | 201 `{ accountId }` |
| POST | `/auth/login` | `{ username, password }` | 200 `TokenResponse` |
| POST | `/auth/refresh` | `{ refreshToken }` | 200 `TokenResponse` (기존 리프레시 토큰은 폐기, 회전 방식) |

`TokenResponse = { accessToken, refreshToken, expiresInSeconds }`. 액세스 토큰 30분, 리프레시 토큰 14일.

## 캐릭터

| 메서드 | 경로 | 요청 | 응답 |
|---|---|---|---|
| GET | `/characters` | - | `CharacterSummaryDto[]` |
| POST | `/characters` | `{ name, classType, gender }` 이름 2~12자 한글·영문·숫자 | 201 `CharacterDto` |
| DELETE | `/characters/{id}` | - | 204 (소프트 삭제, 이름 재사용 가능) |
| GET | `/characters/{id}` | - | `CharacterDto` |
| PUT | `/characters/{id}/position` | `{ x, y, z }` | 204 |
| GET | `/characters/{id}/inventory` | - | `InventoryDto` |
| POST | `/characters/{id}/equip` | `{ itemId }` | `EquipResponse` (같은 부위 장비는 자동 해제) |
| POST | `/characters/{id}/unequip` | `{ itemId }` | `EquipResponse` |
| POST | `/characters/{id}/use-item` | `{ itemId }` 소모품만 | `{ templateId, healHp, healMp, inventory }` (초안에 없던 API. 물약 퀵슬롯·자동 물약용, decisions.md D-27) |
| GET | `/characters/{id}/pk-history` | - | `PkHistoryDto` |

```
CharacterDto = { id, name, classType, gender, level, exp, expToNext, gold,
                 position: {x,y,z}, zone, stats: StatsDto, alignment: AlignmentDto,
                 pkKills, pkDeaths }
StatsDto     = { maxHp, maxMp, attack, defense }
AlignmentDto = { value, state: Innocent|Purple|Chaotic, purpleUntil }
InventoryDto = { capacity, used, gold, items: ItemDto[] }
ItemDto      = { id, templateId, quantity, enhanceLevel, equippedSlot, sellPrice }
EquipResponse = { inventory: InventoryDto, stats: StatsDto }
PkHistoryDto = { kills, deaths, alignment: AlignmentDto,
                 records: [{ killerKind, killerName, victimKind, victimName, victimWasInnocent, alignmentDelta, createdAt }],
                 alignmentHistory: [{ delta, after, reason, createdAt }] }
```

## 상점 (`X-Character-Id`)

| 메서드 | 경로 | 요청 | 응답 |
|---|---|---|---|
| GET | `/shop/items` | - | `{ priceMultiplier, items: [{ templateId, price }] }` 악명이면 배율 1.2 |
| POST | `/shop/buy` | `{ templateId, quantity }` | `InventoryDto` |
| POST | `/shop/sell` | `{ itemId, quantity }` | `InventoryDto` |

## 강화 (`X-Character-Id`)

| 메서드 | 경로 | 요청 | 응답 |
|---|---|---|---|
| POST | `/enhance` | `{ itemId, scrollId }` | `{ result: Success|Fail|Destroy, item: ItemDto?, fromLevel, toLevel, goldSpent, inventory: InventoryDto }` |

## 전투 (`X-Character-Id`)

| 메서드 | 경로 | 요청 | 응답 |
|---|---|---|---|
| POST | `/combat/monster-kill` | `{ monsterId, spawnId, x, z }` monsterId = `Imp` 등 MonsterType | `{ goldGained, expGained, leveledUp, character: CharacterDto }` |
| POST | `/combat/bot-kill` | `{ botId, x, z }` | `{ dropped: ItemDto?, droppedToGround, groundItemId?, alignmentDelta, character: CharacterDto, bot: BotDto }` |
| POST | `/combat/pk-attack` | `{ attackerKind, attackerId, targetKind, targetId, x, z }` 한쪽은 반드시 내 캐릭터 | `{ attacker: AlignmentDto, target: AlignmentDto, attackerBecamePurple }` |
| POST | `/combat/player-death` | `{ killerType: Monster|Bot|Guard, killerId, x, z }` | `{ expLost, dropped: GroundItemDto?, character: CharacterDto }` |

- `bot-kill` 은 선량한 봇이면 처치자 성향치 −300, 전리품 판정, 봇 사망 처리(60초 뒤 부활)를 한 트랜잭션에서 한다.
- `player-death` 는 경험치 손실, 성향 기반 사망 드랍, 봇이 선량한 플레이어를 죽인 경우 봇 성향치 하락, 부활 위치 이동을 한 트랜잭션에서 한다.

## 월드·봇·원장

| 메서드 | 경로 | 요청 | 응답 |
|---|---|---|---|
| GET | `/world/spawns` | - | `SpawnPointDto[]` 몬스터 스폰 표. 클라이언트가 월드 입장 때 받는다 |
| GET | `/world/ground-items?zone=Beginner` | - | `GroundItemDto[]` 만료되지 않은 것만 |
| POST | `/world/ground-items/{id}/pickup` | `{ botId?, x, z }` botId가 있으면 봇이 줍는다 | `{ item: ItemDto, pickedByKind, pickedById, inventory: InventoryDto? }` |
| GET | `/bots?zone=Beginner` | - | `BotDto[]` (zone 생략 시 전체) |
| GET | `/currency/ledger?limit=50` | `X-Character-Id` | `LedgerEntryDto[]` 최신순 |

```
GroundItemDto  = { id, templateId, quantity, enhanceLevel, zone, x, z, expiresAt }
BotDto         = { id, name, classType, gender, level, zone, temperament, alignment: AlignmentDto,
                   isDead, respawnAt, equipment: ItemDto[], itemCount }
LedgerEntryDto = { id, reason, delta, balanceAfter, relatedType, relatedId, createdAt }
SpawnPointDto  = { id, zone, monster, x, z }
```

원장 사유(`reason`)는 `CHARACTER_CREATED`, `MONSTER_KILL`, `SHOP_BUY`, `SHOP_SELL`, `ENHANCE`, `GM_GRANT`, `ADMIN_ADJUST`, `ADMIN_RESET` 이다.

## GM (QA 전용, `ASPNETCORE_ENVIRONMENT=Qa` 일 때만 등록, `X-Character-Id`)

| 메서드 | 경로 | 요청 | 응답 |
|---|---|---|---|
| POST | `/gm/gold` | `{ amount }` 음수면 차감(0 미만 불가) | `CharacterDto` |
| POST | `/gm/item` | `{ templateId, quantity, enhanceLevel }` | `InventoryDto` |
| POST | `/gm/level` | `{ level }` | `CharacterDto` |
| POST | `/gm/enhance-rate` | `{ rates: [{ targetLevel, success, fail, destroy }] }` 또는 `{ reset: true }` | 현재 강화 확률표 |
| POST | `/gm/bot/reset` | `{ zone? }` 해당 구역(생략 시 전체) 봇 삭제 후 재시드 | `{ botCount }` |
| POST | `/gm/alignment` | `{ value, clearPurple }` | `CharacterDto` |
| POST | `/gm/drop-rate` | `{ botLootChance?, botLootChanceChaotic?, deathDrop?: [{ maxAlignment, chance, includeEquipped }], reset? }` | 현재 드랍 설정 |
| GET | `/gm/config` | - | 현재 강화·드랍 설정 전체 |

순간이동은 클라이언트 GM 콘솔이 위치를 옮긴 뒤 `PUT /characters/{id}/position` 으로 저장한다.

## 어드민 (QA 전용, `X-Admin-Key`)

`ASPNETCORE_ENVIRONMENT=Qa` 이고 `Admin:Key`(환경 변수 `Admin__Key`, 16자 이상)를 설정했을 때만 열린다. 그 밖에는 모든 `/admin/*` 가 `404 ADMIN_DISABLED`, 키가 틀리면 `401 UNAUTHORIZED`. 플레이어 토큰은 필요 없다. 브라우저 화면은 `GET /backoffice` (D-36).

| 메서드 | 경로 | 요청 | 응답 |
|---|---|---|---|
| GET | `/admin/accounts?prefix=&limit=` | - | `[{ id, username, createdAt, characterCount }]` |
| GET | `/admin/characters?name=&accountId=&includeDeleted=&limit=` | - | `AdminCharacterDto[]` |
| GET | `/admin/characters/{id}` | - | `CharacterDto` |
| GET | `/admin/characters/{id}/summary` | - | `AdminCharacterDto` |
| GET | `/admin/characters/{id}/inventory` | - | `InventoryDto` |
| GET | `/admin/characters/{id}/ledger?limit=` | - | `LedgerEntryDto[]` 최신순 |
| GET | `/admin/characters/{id}/enhance-history?limit=` | - | `[{ id, itemId, templateId, fromLevel, toLevel, result, goldCost, createdAt }]` |
| GET | `/admin/pk-records?characterId=&limit=` | - | `PkRecordDto[]` |
| GET | `/admin/item-templates` | - | `[{ id, name, type }]` |
| PATCH | `/admin/characters/{id}` | `{ gold?, level?, exp?, alignment?, clearPurple?, x?, z? }` 준 항목만 바꾼다 | `CharacterDto` |
| POST | `/admin/characters/{id}/items` | `{ templateId, quantity, enhanceLevel }` | `InventoryDto` |
| DELETE | `/admin/characters/{id}/items/{itemId}` | - | `InventoryDto` |
| POST | `/admin/characters/{id}/reset` | - | `CharacterDto` 레벨 1·시작 골드·시작 장비·성향 0·마을 |
| GET / POST / DELETE | `/admin/forced` | POST `{ enhance?: ["Success"\|"Fail"\|"Destroy"], botLoot?: [bool], deathDrop?: [bool] }` | `{ enhance, botLoot, deathDrop }` 대기 중인 고정 결과 |
| GET | `/admin/config` | - | `{ enhance, drop, forced }` |
| PUT | `/admin/config/enhance-rates` | GM `enhance-rate` 와 같음 | 강화 설정 |
| PUT | `/admin/config/drop-rates` | GM `drop-rate` 와 같음 | 드랍 설정 |
| POST | `/admin/cleanup` | `{ usernamePrefix }` 3자 이상 | `{ accountsDeleted, charactersDeleted }` |
| POST | `/admin/world/reset` | `{ zone? }` | `{ groundItemsDeleted, botCount }` |
| GET | `/admin/spawns` | - | `SpawnPointDto[]` |
| PUT | `/admin/spawns/{id}` | `{ zone, monster, x, z }` 없으면 만들고 있으면 고친다. 마을 불가 | `SpawnPointDto` |
| DELETE | `/admin/spawns/{id}` | - | `{ count }` 남은 스폰 수 |
| POST | `/admin/spawns/reset` | - | `{ count }` 기본 표(49곳)로 복원 |

```
AdminCharacterDto = { id, accountId, username, name, classType, level, exp, gold, x, z, zone,
                      alignment, purpleUntil, pkKills, pkDeaths, createdAt, deleted }
```

- 골드 변경은 차액만큼 원장에 `ADMIN_ADJUST`, 초기화는 `ADMIN_RESET` 으로 남는다. 원장 합계 = 현재 골드가 유지된다.
- 결과 고정은 다음 판정부터 차례로 쓰인다. 안전 구간(+3까지) 강화는 항상 성공이라 큐를 쓰지 않는다. 큐는 서버 전체 공용이라 테스트를 병렬로 돌리면 서로 가져갈 수 있다.
- 확률·결과 고정은 서버 메모리에만 있어 서버를 다시 켜면 사라진다. 스폰 표는 DB 에 남는다.
