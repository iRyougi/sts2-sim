using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Malaise : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override bool IsXEnergyCost => true;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int amount = Hook.ModifyXValue(CombatState!, this, cardPlay.Resources.EnergyValue) + (IsUpgraded ? 1 : 0);
        await PowerCmd.Apply<StrengthPower>(CombatState!, cardPlay.Target, -amount, Owner.Creature, this);
        await PowerCmd.Apply<WeakPower>(CombatState!, cardPlay.Target, amount, Owner.Creature, this);
    }
}
