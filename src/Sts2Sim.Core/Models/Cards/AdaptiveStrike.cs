using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class AdaptiveStrike : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 18m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Strike];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        CardModel clone = CreateClone();
        clone.SetTemporaryCostOverrideThisCombat(0);
        if (!CombatState!.IsOverOrEnding())
            await CardPileCmd.Generate(CombatState, clone, PileType.Discard, Owner);
    }

    protected override void OnUpgrade() => _damage += 5m;
}
