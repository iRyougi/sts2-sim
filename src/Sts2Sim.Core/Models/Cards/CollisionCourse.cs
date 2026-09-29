using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>10伤害，生成1张Debris进手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.CollisionCourse</c>）。</summary>
public sealed class CollisionCourse : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 10m;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        ICombatState combatState = CombatState!;
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();

        var debris = (Debris)ModelDb.Card<Debris>().MutableClone();
        debris.AssignOwner(Owner);
        await CardPileCmd.Generate(combatState, debris, PileType.Hand);
    }

    protected override void OnUpgrade() => _damage += 4m;
}
