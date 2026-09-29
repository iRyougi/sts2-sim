using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class FlakCannon : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 8m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.RandomEnemy;
    protected override int CanonicalEnergyCost => 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }

    protected override async Task OnPlay(CardPlay play)
    {
        CardModel[] statuses = Owner.PlayerCombatState!.AllPiles
            .Where(pile => pile.Type != PileType.Exhaust)
            .SelectMany(pile => pile.Cards)
            .Where(card => card.Type == CardType.Status).ToArray();
        int hits = statuses.Length;
        foreach (CardModel status in statuses)
            await CardPileCmd.Exhaust(CombatState!, status);
        await DamageCmd.Attack(_damage).WithHitCount(hits).FromCard(this, play)
            .TargetingRandomOpponents(CombatState!).Execute();
    }

    protected override void OnUpgrade() => _damage += 3m;
}
