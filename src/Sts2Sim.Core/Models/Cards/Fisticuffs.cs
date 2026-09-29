using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Fisticuffs : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy,
        true, false, false, Array.Empty<CardKeyword>(),
        7m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        2m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override int CanonicalStarCost => -1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal damage = IsUpgraded ? 9m : 7m;
        var attack = await DamageCmd.Attack(damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();
        decimal block = attack.Results.SelectMany(hit => hit)
            .Sum(result => result.TotalDamage + result.OverkillDamage);
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, block, ValueProp.Move, this, cardPlay);
    }
}
