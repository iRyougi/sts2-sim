using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>SleightOfFlesh</c>：2 费能力，获得 9（升级 13）层 <see cref="SleightOfFleshPower"/>。</summary>
public sealed class SleightOfFlesh : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 2;

    private decimal PowerAmount => IsUpgraded ? 13m : 9m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<SleightOfFleshPower>(CombatState!, Owner.Creature, PowerAmount, Owner.Creature, this);
}
