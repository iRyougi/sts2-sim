using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class MementoMori : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 19m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.RandomEnemy;
    protected override int CanonicalEnergyCost => 3;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Sly];
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        var hittableEnemies = CombatState!.HittableEnemies;
        if (hittableEnemies.Count != 0)
        {
            var target = hittableEnemies.MinBy(creature => creature.CurrentHp)!;
            await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(target).Execute();
        }
    }
    protected override void OnUpgrade() => _damage += 4m;
}
