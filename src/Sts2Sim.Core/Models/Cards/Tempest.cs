using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Tempest : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override bool IsXEnergyCost => true;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int count = Hook.ModifyXValue(CombatState!, this, cardPlay.Resources.EnergyValue)
            + (IsUpgraded ? 1 : 0);
        for (int i = 0; i < count; i++)
            await OrbCmd.Channel<LightningOrb>(CombatState!, Owner);
    }
}
