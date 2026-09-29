using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Expose : CardModel
{
    private decimal Vulnerable => IsUpgraded ? 3m : 2m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await CreatureCmd.LoseBlock(CombatState!, play.Target, play.Target.Block, Owner.Creature);
        if (play.Target.GetPower<ArtifactPower>() is { } artifact) await PowerCmd.Remove(artifact);
        await PowerCmd.Apply<VulnerablePower>(CombatState!, play.Target, Vulnerable, Owner.Creature, this);
    }

}
