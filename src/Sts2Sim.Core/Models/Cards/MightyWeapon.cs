using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>v0.113.1 Shrine of the Warlord card (#927).</summary>
public sealed class MightyWeapon : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const int DazedCount = 2;
    private decimal _damage = 25m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage, Cards: DazedCount);
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();
        for (int index = 0; index < DazedCount; index++)
        {
            var dazed = (Dazed)ModelDb.Card<Dazed>().MutableClone();
            dazed.AssignOwner(Owner);
            await CardPileCmd.Generate(CombatState!, dazed, PileType.Draw, Owner, CardPilePosition.Random);
        }
    }

    protected override void OnUpgrade() => _damage += 5m;
}
