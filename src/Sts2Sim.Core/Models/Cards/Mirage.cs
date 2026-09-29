using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Mirage : CardModel
{
    public override bool GainsBlock => true;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override Task OnPlay(CardPlay play) => CreatureCmd.GainBlock(CombatState!, Owner.Creature,
        CombatState!.Enemies.Where(enemy => enemy.IsAlive).Sum(enemy => enemy.Powers.OfType<PoisonPower>().Sum(power => power.Amount)),
        ValueProp.Move, this, play);
    protected override void OnUpgrade() => RemoveKeywordInternal(CardKeyword.Exhaust);
}
