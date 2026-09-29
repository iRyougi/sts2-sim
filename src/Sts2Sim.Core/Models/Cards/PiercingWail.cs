using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class PiercingWail : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal amount = IsUpgraded ? 8m : 6m;
        foreach (var enemy in CombatState!.HittableEnemies.ToArray())
        {
            await PowerCmd.Apply<PiercingWailPower>(CombatState, enemy, amount, Owner.Creature, this);
        }
    }
}
