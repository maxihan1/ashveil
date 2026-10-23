// QA 어드민이 미리 정해 둔 확률 결과(강화·봇 전리품·사망 드랍)를 차례로 꺼내 쓰는 큐
using System.Collections.Concurrent;
using Ashveil.Shared;

namespace Ashveil.Server.Infrastructure;

/// <summary>
/// 비어 있으면 평소처럼 난수로 판정한다. 큐는 서버 전체 공용이라 여러 테스트를 동시에 돌리면 서로의 결과를 가져갈 수 있다.
/// ponytail: 프로세스 메모리 전역 큐. 테스트를 병렬로 돌려야 하면 캐릭터별 큐로 나눈다.
/// </summary>
public sealed class ForcedOutcomes
{
    private readonly ConcurrentQueue<EnhanceResult> _enhance = new();
    private readonly ConcurrentQueue<bool> _botLoot = new();
    private readonly ConcurrentQueue<bool> _deathDrop = new();

    public void Enqueue(IEnumerable<EnhanceResult>? enhance, IEnumerable<bool>? botLoot, IEnumerable<bool>? deathDrop)
    {
        foreach (var r in enhance ?? []) _enhance.Enqueue(r);
        foreach (var d in botLoot ?? []) _botLoot.Enqueue(d);
        foreach (var d in deathDrop ?? []) _deathDrop.Enqueue(d);
    }

    public void Clear()
    {
        _enhance.Clear();
        _botLoot.Clear();
        _deathDrop.Clear();
    }

    public bool TryNextEnhance(out EnhanceResult result) => _enhance.TryDequeue(out result);

    public DropRoll BotLoot(DropRoll rolled) => _botLoot.TryDequeue(out var drops) ? new DropRoll(drops, rolled.IncludeEquipped) : rolled;

    public DropRoll DeathDrop(DropRoll rolled) => _deathDrop.TryDequeue(out var drops) ? new DropRoll(drops, rolled.IncludeEquipped) : rolled;

    public (EnhanceResult[] Enhance, bool[] BotLoot, bool[] DeathDrop) Snapshot() => (_enhance.ToArray(), _botLoot.ToArray(), _deathDrop.ToArray());
}
