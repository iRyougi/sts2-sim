using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models.Cards;

public sealed class MultiCast : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override bool IsXEnergyCost => true;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay play)
    {
        int evokes = Hook.ModifyXValue(CombatState!, this, play.Resources.EnergyXValue);
        if (IsUpgraded) evokes++;
        for (int i = 0; i < evokes; i++)
            await OrbCmd.EvokeNext(CombatState!, Owner, dequeue: i == evokes - 1);
    }
}
