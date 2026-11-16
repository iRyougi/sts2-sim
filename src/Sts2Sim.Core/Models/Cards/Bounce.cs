using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>施加 StretchPower；若上一玩家回合打出并结算本牌，抽牌前回到手牌（原版 <c>Bounce</c>）。</summary>
public sealed class Bounce : CardModel, ICardChoiceBaseValueProvider
{
    private int _lastFinishedPlayerTurn = -1;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    private decimal StretchAmount => IsUpgraded ? 13m : 10m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<StretchPower>(CombatState!, Owner.Creature, StretchAmount, Owner.Creature, this);
        if (Owner.PlayerCombatState is { } pcs)
        {
            _lastFinishedPlayerTurn = pcs.TurnNumber;
        }
    }

    public override Task BeforeHandDraw(Player player)
    {
        if (ReferenceEquals(player, Owner) &&
            _lastFinishedPlayerTurn >= 0 &&
            player.PlayerCombatState is { } pcs &&
            pcs.TurnNumber - 1 == _lastFinishedPlayerTurn &&
            Pile?.Type != PileType.Hand)
        {
            CardPileCmd.Add(this, PileType.Hand);
        }

        return Task.CompletedTask;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _lastFinishedPlayerTurn = -1;
    }

    internal override void RestoreCombatCloneReferencesFrom(
        CardModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        _lastFinishedPlayerTurn = ((Bounce)source)._lastFinishedPlayerTurn;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        int turnNumber = Owner?.PlayerCombatState?.TurnNumber ?? -1;
        bool playedThisTurn = _lastFinishedPlayerTurn >= 0 && turnNumber == _lastFinishedPlayerTurn;
        builder.Append(playedThisTurn);
    }
}
