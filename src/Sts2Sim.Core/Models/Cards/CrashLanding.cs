using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>21伤害(全体)，Debris补满手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.CrashLanding</c>）。</summary>
public sealed class CrashLanding : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 21m;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AllEnemies;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ICombatState combatState = CombatState!;
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).TargetingAllOpponents(combatState).Execute();

        int slotsToFill = CardPile.MaxCardsInHand - Owner.PlayerCombatState!.Hand.Cards.Count;
        for (int i = 0; i < slotsToFill; i++)
        {
            var debris = (Debris)ModelDb.Card<Debris>().MutableClone();
            debris.AssignOwner(Owner);
            await CardPileCmd.Generate(combatState, debris, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => _damage += 5m;
}
