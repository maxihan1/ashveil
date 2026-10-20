// 트랜잭션, 소유권 잠금, 골드 원장, 아이템 적재·이동 같은 공용 상태 변경 연산
using Ashveil.Server.Data;
using Ashveil.Server.Infrastructure;
using Ashveil.Shared;
using Ashveil.Shared.Dto;
using Microsoft.EntityFrameworkCore;

namespace Ashveil.Server.Services;

public static class GameOps
{
    /// <summary>work 안의 변경을 한 트랜잭션으로 저장한다. 행 잠금은 커밋할 때 풀린다.</summary>
    public static async Task InTransactionAsync(this GameDbContext db, Func<Task> work)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await work();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public static async Task<Character> LockOwnedCharacterAsync(this GameDbContext db, long characterId, long accountId)
    {
        var character = await db.LockCharacterAsync(characterId) ?? throw GameErrors.NotFound("캐릭터");
        if (character.AccountId != accountId) throw GameErrors.Forbidden();
        return character;
    }

    public static async Task<Character> GetOwnedCharacterAsync(this GameDbContext db, long characterId, long accountId)
    {
        var character = await db.Characters.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == characterId && c.DeletedAt == null) ?? throw GameErrors.NotFound("캐릭터");
        if (character.AccountId != accountId) throw GameErrors.Forbidden();
        return character;
    }

    /// <summary>골드를 바꾸고 같은 트랜잭션에 원장 행을 남긴다.</summary>
    public static void ChangeGold(this GameDbContext db, Character character, long delta, string reason,
        string? relatedType, string? relatedId, DateTime now)
    {
        if (character.Gold + delta < 0) throw GameErrors.Conflict(ErrorCodes.InsufficientGold);
        character.Gold += delta;
        db.LedgerEntries.Add(new LedgerEntry
        {
            CharacterId = character.Id,
            Reason = reason,
            Delta = delta,
            BalanceAfter = character.Gold,
            RelatedType = relatedType,
            RelatedId = relatedId,
            CreatedAt = now,
        });
    }

    public static void RecordAlignment(this GameDbContext db, EntityKind kind, long id, int delta, int after, string reason, DateTime now)
    {
        if (delta == 0) return;
        db.AlignmentHistories.Add(new AlignmentHistory
        {
            EntityKind = kind, EntityId = id, Delta = delta, After = after, Reason = reason, CreatedAt = now,
        });
    }

    public static Task<List<Item>> LoadItemsAsync(this GameDbContext db, OwnerKind kind, long ownerId) =>
        db.Items.Where(i => i.OwnerKind == kind && i.OwnerId == ownerId).OrderBy(i => i.Id).ToListAsync();

    public static AddPlan Plan(List<Item> ownerItems, string templateId, int quantity) =>
        InventoryRules.PlanAdd(ownerItems.Select(i => new StackView(i.Id, i.TemplateId, i.Quantity)).ToList(), templateId, quantity);

    /// <summary>새 아이템을 만든다(구매, GM 지급, 봇 시드). 계획이 맞지 않으면 호출하지 않는다.</summary>
    public static Item AddNewItems(this GameDbContext db, OwnerKind kind, long ownerId, List<Item> ownerItems,
        AddPlan plan, string templateId, int enhanceLevel, DateTime now)
    {
        Item? landed = null;
        foreach (var (itemId, add) in plan.Merges)
        {
            var stack = ownerItems.First(i => i.Id == itemId);
            stack.Quantity += add;
            landed ??= stack;
        }
        foreach (var quantity in plan.NewStacks)
        {
            var item = new Item
            {
                TemplateId = templateId, Quantity = quantity, EnhanceLevel = enhanceLevel,
                OwnerKind = kind, OwnerId = ownerId, CreatedAt = now,
            };
            db.Items.Add(item);
            ownerItems.Add(item);
            landed ??= item;
        }
        return landed!;
    }

    /// <summary>
    /// 기존 아이템 인스턴스를 다른 소유자에게 옮긴다. 겹치는 아이템은 받는 쪽 스택에 먼저 합치고,
    /// 다 합쳐지면 원래 인스턴스는 지운다. 소유자 컬럼을 바꾸는 방식이라 복제가 생기지 않는다.
    /// </summary>
    public static Item MoveItem(this GameDbContext db, Item item, OwnerKind kind, long ownerId, List<Item> ownerItems, AddPlan plan)
    {
        item.EquippedSlot = null;
        Item? landed = null;
        foreach (var (itemId, add) in plan.Merges)
        {
            var stack = ownerItems.First(i => i.Id == itemId);
            stack.Quantity += add;
            landed ??= stack;
        }
        if (plan.NewStacks.Count == 0)
        {
            db.Items.Remove(item);
            return landed!;
        }

        item.OwnerKind = kind;
        item.OwnerId = ownerId;
        item.Quantity = plan.NewStacks[0];
        ownerItems.Add(item);
        foreach (var quantity in plan.NewStacks.Skip(1))
        {
            var extra = new Item
            {
                TemplateId = item.TemplateId, Quantity = quantity, EnhanceLevel = item.EnhanceLevel,
                OwnerKind = kind, OwnerId = ownerId, CreatedAt = item.CreatedAt,
            };
            db.Items.Add(extra);
            ownerItems.Add(extra);
        }
        return landed ?? item;
    }

    /// <summary>아이템을 바닥에 내려놓는다(사망 드랍, 인벤토리가 찬 상태의 전리품).</summary>
    public static GroundItem DropToGround(this GameDbContext db, Item item, float x, float z, EntityKind byKind, long byId,
        int lifetimeSeconds, DateTime now)
    {
        item.EquippedSlot = null;
        item.OwnerKind = OwnerKind.Ground;
        item.OwnerId = 0;
        var ground = new GroundItem
        {
            Item = item,
            Zone = ZoneMap.GetZone(x, z),
            X = ZoneMap.ClampToWorld(x),
            Z = ZoneMap.ClampToWorld(z),
            DroppedAt = now,
            ExpiresAt = now.AddSeconds(lifetimeSeconds),
            DroppedByKind = byKind,
            DroppedById = byId,
        };
        db.GroundItems.Add(ground);
        return ground;
    }

    public static Stats ComputeStats(ClassType classType, int level, IEnumerable<Item> items) =>
        StatRules.Compute(classType, level,
            items.Where(i => i.EquippedSlot != null).Select(i => new EquippedItem(i.TemplateId, i.EnhanceLevel)));

    public static async Task<CharacterDto> CharacterDtoAsync(this GameDbContext db, Character character, DateTime now)
    {
        var equipped = await db.Items.AsNoTracking()
            .Where(i => i.OwnerKind == OwnerKind.Character && i.OwnerId == character.Id && i.EquippedSlot != null)
            .ToListAsync();
        return Map.Character(character, equipped, now);
    }

    public static async Task<InventoryDto> InventoryDtoAsync(this GameDbContext db, Character character)
    {
        var items = await db.Items.AsNoTracking()
            .Where(i => i.OwnerKind == OwnerKind.Character && i.OwnerId == character.Id)
            .OrderBy(i => i.Id).ToListAsync();
        return Map.Inventory(character, items);
    }

    /// <summary>보고 좌표가 대상 구역 안쪽(또는 경계에서 허용 오차 이내)인지.</summary>
    public static bool IsNearZone(ZoneId zone, float x, float z, float tolerance)
    {
        if (ZoneMap.GetZone(x, z) == zone) return true;
        var b = ZoneMap.BoundsOf(zone);
        var dx = Math.Max(Math.Max(b.MinX - x, 0), x - b.MaxX);
        var dz = Math.Max(Math.Max(b.MinZ - z, 0), z - b.MaxZ);
        return dx * dx + dz * dz <= tolerance * tolerance;
    }

    public static void RequireFinite(params float[] values)
    {
        if (values.Any(v => float.IsNaN(v) || float.IsInfinity(v))) throw GameErrors.Validation("좌표 값이 올바르지 않습니다.");
    }
}
