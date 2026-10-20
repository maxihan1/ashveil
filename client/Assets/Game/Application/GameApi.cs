// 서버 API 엔드포인트별 타입 지정 호출 모음 (docs/api.md 와 1:1)
using System.Collections.Generic;
using System.Threading.Tasks;
using Ashveil.Shared;
using Ashveil.Shared.Dto;

namespace Ashveil.Application
{
    public sealed class GameApi
    {
        private readonly ApiClient _client;
        private readonly Session _session;

        public GameApi(ApiClient client, Session session)
        {
            _client = client;
            _session = session;
        }

        private string Me => "/characters/" + _session.CharacterId;

        // 인증
        public Task<SignupResponse> SignupAsync(string username, string password) =>
            _client.SendAsync<SignupResponse>("POST", "/auth/signup", new SignupRequest { Username = username, Password = password }, auth: false);

        public async Task LoginAsync(string username, string password)
        {
            var tokens = await _client.SendAsync<TokenResponse>("POST", "/auth/login",
                new LoginRequest { Username = username, Password = password }, auth: false);
            _session.Username = username;
            _session.Apply(tokens);
        }

        // 캐릭터
        public Task<List<CharacterSummaryDto>> ListCharactersAsync() => _client.SendAsync<List<CharacterSummaryDto>>("GET", "/characters");

        public Task<CharacterDto> CreateCharacterAsync(string name, ClassType classType, Gender gender) =>
            _client.SendAsync<CharacterDto>("POST", "/characters", new CreateCharacterRequest { Name = name, ClassType = classType, Gender = gender });

        public Task DeleteCharacterAsync(long id) => _client.SendAsync("DELETE", "/characters/" + id);

        public Task<CharacterDto> GetCharacterAsync() => _client.SendAsync<CharacterDto>("GET", Me);

        public Task SavePositionAsync(float x, float y, float z) =>
            _client.SendAsync("PUT", Me + "/position", new PositionRequest { X = x, Y = y, Z = z });

        public Task<InventoryDto> GetInventoryAsync() => _client.SendAsync<InventoryDto>("GET", Me + "/inventory");

        public Task<EquipResponse> EquipAsync(long itemId) => _client.SendAsync<EquipResponse>("POST", Me + "/equip", new EquipRequest { ItemId = itemId });

        public Task<EquipResponse> UnequipAsync(long itemId) => _client.SendAsync<EquipResponse>("POST", Me + "/unequip", new EquipRequest { ItemId = itemId });

        public Task<UseItemResponse> UseItemAsync(long itemId) => _client.SendAsync<UseItemResponse>("POST", Me + "/use-item", new UseItemRequest { ItemId = itemId });

        public Task<PkHistoryDto> GetPkHistoryAsync() => _client.SendAsync<PkHistoryDto>("GET", Me + "/pk-history");

        // 상점·강화·원장
        public Task<ShopItemsResponse> GetShopItemsAsync() => _client.SendAsync<ShopItemsResponse>("GET", "/shop/items");

        public Task<InventoryDto> BuyAsync(string templateId, int quantity) =>
            _client.SendAsync<InventoryDto>("POST", "/shop/buy", new BuyRequest { TemplateId = templateId, Quantity = quantity });

        public Task<InventoryDto> SellAsync(long itemId, int quantity) =>
            _client.SendAsync<InventoryDto>("POST", "/shop/sell", new SellRequest { ItemId = itemId, Quantity = quantity });

        public Task<EnhanceResponse> EnhanceAsync(long itemId, long scrollId) =>
            _client.SendAsync<EnhanceResponse>("POST", "/enhance", new EnhanceRequest { ItemId = itemId, ScrollId = scrollId });

        public Task<List<LedgerEntryDto>> GetLedgerAsync(int limit = 50) => _client.SendAsync<List<LedgerEntryDto>>("GET", "/currency/ledger?limit=" + limit);

        // 전투
        public Task<MonsterKillResponse> ReportMonsterKillAsync(MonsterType monster, string spawnId, float x, float z) =>
            _client.SendAsync<MonsterKillResponse>("POST", "/combat/monster-kill",
                new MonsterKillRequest { MonsterId = monster, SpawnId = spawnId, X = x, Z = z });

        public Task<BotKillResponse> ReportBotKillAsync(long botId, float x, float z) =>
            _client.SendAsync<BotKillResponse>("POST", "/combat/bot-kill", new BotKillRequest { BotId = botId, X = x, Z = z });

        public Task<PkAttackResponse> ReportPkAttackAsync(EntityKind attackerKind, long attackerId, EntityKind targetKind, long targetId, float x, float z) =>
            _client.SendAsync<PkAttackResponse>("POST", "/combat/pk-attack", new PkAttackRequest
            {
                AttackerKind = attackerKind, AttackerId = attackerId, TargetKind = targetKind, TargetId = targetId, X = x, Z = z,
            });

        public Task<PlayerDeathResponse> ReportPlayerDeathAsync(EntityKind killerType, long? killerId, float x, float z) =>
            _client.SendAsync<PlayerDeathResponse>("POST", "/combat/player-death",
                new PlayerDeathRequest { KillerType = killerType, KillerId = killerId, X = x, Z = z });

        // 월드·봇
        public Task<List<GroundItemDto>> GetGroundItemsAsync(ZoneId zone) =>
            _client.SendAsync<List<GroundItemDto>>("GET", "/world/ground-items?zone=" + zone);

        public Task<PickupResponse> PickupAsync(long groundItemId, long? botId, float x, float z) =>
            _client.SendAsync<PickupResponse>("POST", "/world/ground-items/" + groundItemId + "/pickup",
                new PickupRequest { BotId = botId, X = x, Z = z });

        public Task<List<BotDto>> GetBotsAsync() => _client.SendAsync<List<BotDto>>("GET", "/bots");

        // GM (QA 서버 전용)
        public Task<CharacterDto> GmGoldAsync(long amount) => _client.SendAsync<CharacterDto>("POST", "/gm/gold", new GmGoldRequest { Amount = amount });

        public Task<InventoryDto> GmItemAsync(string templateId, int quantity, int enhance) =>
            _client.SendAsync<InventoryDto>("POST", "/gm/item", new GmItemRequest { TemplateId = templateId, Quantity = quantity, EnhanceLevel = enhance });

        public Task<CharacterDto> GmLevelAsync(int level) => _client.SendAsync<CharacterDto>("POST", "/gm/level", new GmLevelRequest { Level = level });

        public Task<EnhanceConfig> GmEnhanceRateAsync(List<EnhanceRate> rates, bool reset) =>
            _client.SendAsync<EnhanceConfig>("POST", "/gm/enhance-rate", new GmEnhanceRateRequest { Rates = rates, Reset = reset });

        public Task<GmBotResetResponse> GmBotResetAsync(ZoneId? zone) =>
            _client.SendAsync<GmBotResetResponse>("POST", "/gm/bot/reset", new GmBotResetRequest { Zone = zone });

        public Task<CharacterDto> GmAlignmentAsync(int value) =>
            _client.SendAsync<CharacterDto>("POST", "/gm/alignment", new GmAlignmentRequest { Value = value, ClearPurple = true });

        public Task<DropConfig> GmDropRateAsync(double? botLoot, double? botLootChaotic, List<DeathDropTier> deathDrop, bool reset) =>
            _client.SendAsync<DropConfig>("POST", "/gm/drop-rate", new GmDropRateRequest
            {
                BotLootChance = botLoot, BotLootChanceChaotic = botLootChaotic, DeathDrop = deathDrop, Reset = reset,
            });

        public Task<GmConfigResponse> GmConfigAsync() => _client.SendAsync<GmConfigResponse>("GET", "/gm/config");
    }
}
