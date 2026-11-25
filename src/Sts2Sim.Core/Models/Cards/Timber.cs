using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>造成 20（升级 25）点伤害；若击杀目标则对全体可命中敌人再造成 30（升级 35）点伤害。
/// v0.114.0 原版 <c>Timber</c> 把两段伤害放进同一个 <c>AttackContext</c>（只算一次攻击）。</summary>
public sealed class Timber : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 3;
    private decimal Damage => IsUpgraded ? 25m : 20m;
    private decimal OnKillDamage => IsUpgraded ? 35m : 30m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await using AttackContext context = await AttackCommand.CreateContextAsync(CombatState!, cardPlay);
        IReadOnlyList<DamageResult> results = await CreatureCmd.Damage(
            CombatState!, [cardPlay.Target], Damage, ValueProp.Move, Owner.Creature, this, cardPlay);
        context.AddHit(results);
        if (results.Any(result => result.WasTargetKilled))
        {
            Creature[] targets = CombatState!.GetOpponentsOf(Owner.Creature)
                .Where(enemy => enemy.IsHittable)
                .ToArray();
            context.AddHit(await CreatureCmd.Damage(
                CombatState, targets, OnKillDamage, ValueProp.Move, Owner.Creature, this, cardPlay));
        }
    }
}
