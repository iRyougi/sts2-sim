using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Orbs;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Darkness : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await OrbCmd.Channel<DarkOrb>(CombatState!, Owner);
        int triggerCount = IsUpgraded ? 2 : 1;
        IEnumerable<OrbModel> darkOrbs = Owner.PlayerCombatState!.OrbQueue.Orbs.Where(orb => orb is DarkOrb);
        foreach (OrbModel orb in darkOrbs)
            for (int i = 0; i < triggerCount; i++)
                await OrbCmd.Passive(CombatState!, orb);
    }
}
