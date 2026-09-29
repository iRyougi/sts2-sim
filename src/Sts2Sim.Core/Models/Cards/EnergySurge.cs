using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class EnergySurge : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _energy = 2m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: (double)_energy);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AllAllies;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        foreach (Creature creature in CombatState!.GetCreaturesOnSide(Owner.Creature.Side)
                     .Where(creature => creature.IsAlive && creature.IsPlayer))
            await PlayerCmd.GainEnergy(_energy, creature.Player!);
    }
    protected override void OnUpgrade() => _energy += 1m;
}
