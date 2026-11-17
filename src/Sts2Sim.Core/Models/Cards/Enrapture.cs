using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>仅多人牌：对目标友方施加 Vigor 与 Stretch（原版 <c>Enrapture</c>）。</summary>
public sealed class Enrapture : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyAlly;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 1;
    private decimal Amount => IsUpgraded ? 9m : 6m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<VigorPower>(CombatState!, cardPlay.Target, Amount, Owner.Creature, this);
        await PowerCmd.Apply<StretchPower>(CombatState!, cardPlay.Target, Amount, Owner.Creature, this);
    }
}
