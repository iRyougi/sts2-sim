using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Yoga : CardModel, ICardChoiceBaseValueProvider
{
    private decimal Stretch => IsUpgraded ? 8m : 6m;

    public override bool IsMultiplayerOnly => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AllAllies;
    protected override int CanonicalEnergyCost => 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        List<Creature> teammates = CombatState!.GetCreaturesOnSide(Owner.Creature.Side)
            .Where(c => c.IsAlive && c.IsPlayer)
            .ToList();
        foreach (Creature teammate in teammates)
        {
            await PowerCmd.Apply<StretchPower>(CombatState!, teammate, Stretch, Owner.Creature, this);
        }
    }
}
