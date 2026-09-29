using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Chaos : CardModel, ICardChoiceBaseValueProvider
{
    private int _repeat = 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        for (int i = 0; i < _repeat; i++)
            await OrbCmd.Channel(CombatState!,
                OrbModel.GetRandomOrb(Owner.RunState.Rng.CombatOrbGeneration).ToMutable(), Owner);
    }

    protected override void OnUpgrade() => _repeat += 1;
}
