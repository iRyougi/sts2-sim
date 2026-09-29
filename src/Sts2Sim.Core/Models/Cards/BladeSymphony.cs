using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BladeSymphony : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AllAllies;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 2;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        foreach (Creature teammate in CombatState!.Allies.Where(c =>
                     c.IsAlive && c.IsPlayer && !ReferenceEquals(c, Owner.Creature)))
        {
            await Shiv.CreateInHand(teammate.Player!, 2, CombatState!);
        }
    }
    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
