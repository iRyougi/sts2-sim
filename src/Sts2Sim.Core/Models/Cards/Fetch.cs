using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Fetch</c>：Osty 对目标造成 3（升级 6）伤害；若这张牌本回合还没有打出完毕过，抽 1。
/// Osty 不在时无效果。</summary>
public sealed class Fetch : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    // 原版 HasBeenPlayedThisTurn 查 CardPlayFinishedEntry(Card == this).HappenedThisTurn：
    // 这里记下本实例最近一次打出完毕时的回合键（轮次、行动方、各玩家回合数）。
    // 原版历史只认同一对象，所以 CreateClone/CreateDupe 出来的新实例不继承（AfterCloned 清空），
    // 战斗状态克隆则由 RestoreCombatCloneReferencesFrom 原样复制。
    private int _finishedRound = -1;
    private CombatSide _finishedSide;
    private int?[]? _finishedTurnNumbers;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal OstyDamage => IsUpgraded ? 6m : 3m;

    private const int Cards = 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = OstyDamage;
        return true;
    }

    private bool HasBeenPlayedThisTurn
    {
        get
        {
            if (_finishedTurnNumbers is null || CombatState is not { } state ||
                _finishedRound != state.RoundNumber || _finishedSide != state.CurrentSide ||
                _finishedTurnNumbers.Length != state.Players.Count)
                return false;
            for (int i = 0; i < _finishedTurnNumbers.Length; i++)
            {
                if (state.Players[i].PlayerCombatState?.TurnNumber != _finishedTurnNumbers[i])
                    return false;
            }
            return true;
        }
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (!Owner.IsOstyMissing)
        {
            await DamageCmd.Attack(OstyDamage).FromOsty(Owner.Osty!, this, cardPlay)
                .Targeting(cardPlay.Target).Execute();
            if (!HasBeenPlayedThisTurn)
                await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
        }

        // 原版在 OnPlay（及附魔/诅咒的 OnPlay）之后记 CardPlayFinishedEntry；其间无人读取本卡的这条历史。
        RecordPlayFinished();
    }

    private void RecordPlayFinished()
    {
        if (CombatState is not { } state)
            return;
        _finishedRound = state.RoundNumber;
        _finishedSide = state.CurrentSide;
        _finishedTurnNumbers = state.Players.Select(player => player.PlayerCombatState?.TurnNumber).ToArray();
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _finishedRound = -1;
        _finishedSide = default;
        _finishedTurnNumbers = null;
    }

    internal override void RestoreCombatCloneReferencesFrom(
        CardModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        var sourceFetch = (Fetch)source;
        _finishedRound = sourceFetch._finishedRound;
        _finishedSide = sourceFetch._finishedSide;
        _finishedTurnNumbers = sourceFetch._finishedTurnNumbers?.ToArray();
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        builder.Append(HasBeenPlayedThisTurn);
}
