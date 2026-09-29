using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Quadcast : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        if (Owner.PlayerCombatState!.OrbQueue.Orbs.Count == 0) return;
        for (int i = 0; i < 4; i++)
            await OrbCmd.EvokeNext(CombatState!, Owner, dequeue: i == 3);
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
