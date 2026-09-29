using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Cards;

public sealed class HowlFromBeyond : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);

    private decimal _damage = 18m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 3;

    protected override Task OnPlay(CardPlay cardPlay) =>
        DamageCmd.Attack(_damage).FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!).Execute();

    public override Task AfterAutoPostPlayPhaseEntered(Player player) =>
        Pile?.Type == PileType.Exhaust && player == Owner
            ? AutoPlayCmd.FromCards(CombatState!, Owner, [this])
            : Task.CompletedTask;

    protected override void OnUpgrade() => _damage += 6m;
}
