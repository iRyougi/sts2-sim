using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Entities.Players;

/// <summary>Immutable run-entry snapshot of card and timeline unlocks for one player.</summary>
public sealed class PlayerUnlockState
{
    private readonly HashSet<ModelId> _unlockedColorlessCardIds;

    /// <summary><c>null</c> 表示**全部 epoch 均已揭示**，而不是"一个都没揭示"。
    ///
    /// 这里刻意不保存一份 epoch id 清单。曾经有过一份手写清单（`AllRegentEpochIds`），
    /// 于是每新增一个角色或池都必须记得往里补一条——Plan 08b-2 换成静默猎手时没补，
    /// `SILENT4_EPOCH` 缺席导致猎手专属药水被解锁门禁整个挡掉（偏离 #299），
    /// 而这类遗漏不会有任何编译或测试报错。
    ///
    /// 本项目的既定假设是**永远以全成就全解锁的完美存档运行**，所以"全解锁"应当表示成一个状态，
    /// 而不是一份需要与内容同步维护的枚举。想模拟部分解锁的测试仍可用带
    /// <paramref name="revealedEpochIds"/> 的构造函数显式传入子集。</summary>
    private readonly HashSet<string>? _revealedEpochIds;

    /// <summary>全 epoch 揭示（本项目默认）。彩色卡解锁集仍需显式传入。</summary>
    public PlayerUnlockState(IEnumerable<ModelId> unlockedColorlessCardIds)
    {
        ArgumentNullException.ThrowIfNull(unlockedColorlessCardIds);
        _unlockedColorlessCardIds = unlockedColorlessCardIds.ToHashSet();
        _revealedEpochIds = null;
    }

    /// <summary>只揭示指定 epoch。仅用于刻意验证解锁门禁的测试；生产路径不要用。</summary>
    public PlayerUnlockState(
        IEnumerable<ModelId> unlockedColorlessCardIds,
        IEnumerable<string> revealedEpochIds)
    {
        ArgumentNullException.ThrowIfNull(unlockedColorlessCardIds);
        ArgumentNullException.ThrowIfNull(revealedEpochIds);
        _unlockedColorlessCardIds = unlockedColorlessCardIds.ToHashSet();
        _revealedEpochIds = revealedEpochIds.ToHashSet(StringComparer.Ordinal);
    }

    public bool IsColorlessCardUnlocked(CardModel card) =>
        card.IsColorless && _unlockedColorlessCardIds.Contains(card.Id);

    public bool IsEpochRevealed(string epochId) =>
        _revealedEpochIds is null || _revealedEpochIds.Contains(epochId);

    /// <summary>全部角色卡池（上游 <c>PlayerUnlockState.CharacterCardPools</c>）。
    ///
    /// 由 <see cref="ModelDb"/> 发现，**自动随新角色增长**——与 <see cref="_revealedEpochIds"/>
    /// 同一条原则：全解锁是状态，不是需要手工同步的清单。供"从非本角色卡池生成"一类效果
    /// （<c>Splash</c>、<c>Kaleidoscope</c>）使用。</summary>
    public IEnumerable<CardPoolModel> CharacterCardPools =>
        ModelDb.AllCharacters.Select(character => character.CardPool);

    /// <summary>全解锁存档：所有无色卡（由 <see cref="ModelDb"/> 发现，自动随内容增长）
    /// 与全部 epoch。</summary>
    public static PlayerUnlockState AllUnlocked() =>
        new(ModelDb.All<CardModel>()
            .Where(card => card.IsColorless)
            .Select(card => card.Id));
}
