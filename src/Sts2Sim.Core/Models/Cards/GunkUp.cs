using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class GunkUp : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 4m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }

    protected override async Task OnPlay(CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(_damage).WithHitCount(3).FromCard(this, play)
            .Targeting(play.Target).Execute();
        var slimed = (Slimed)ModelDb.Card<Slimed>().MutableClone();
        slimed.AssignOwner(Owner);
        await CardPileCmd.Generate(CombatState!, slimed, PileType.Discard, Owner);
    }

    protected override void OnUpgrade() => _damage += 1m;
}
