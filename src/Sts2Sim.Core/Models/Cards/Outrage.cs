using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Outrage : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);

    private decimal _damage = 9m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        if (CombatState is null)
        {
            return;
        }
        foreach (Player teammate in CombatState.Allies
                     .Where(creature => creature.IsAlive && creature.IsPlayer && creature != Owner.Creature)
                     .Select(creature => creature.Player!))
        {
            CardModel clone = CreateClone();
            clone.AssignOwner(teammate);
            await CardPileCmd.Generate(CombatState, clone, PileType.Discard, Owner);
        }
    }

    protected override void OnUpgrade() => _damage += 4m;
}
