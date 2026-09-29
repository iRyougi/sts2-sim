using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Misery</c>：先记下目标身上的全部减益（按当前层数判定类型）及层数，造成 7（升级 9）伤害，
/// 再把记下的减益按原施加者复制给其余每个可命中的敌人。升级获得保留。
/// 临时能力（<see cref="ITemporaryPower"/>，如临时减力）的层数会并入它内部施加的那条能力的记录里，
/// 使二者合计为 0 而跳过，只由临时能力自己去复制，避免重复。</summary>
public sealed class Misery : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    private decimal Damage => IsUpgraded ? 9m : 7m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        // 原版 ToDictionary 的插入顺序即目标能力顺序；这里用列表保持同一顺序。快照是攻击前的实例克隆，
        // 复制时再克隆一次，新实例保留层数以外的状态（如 SlowPower 的已出牌计数）。
        List<(PowerModel Power, int Amount)> debuffs = cardPlay.Target.Powers
            .Where(power => power.GetTypeForAmount(power.Amount) == PowerType.Debuff)
            .Select(power => ((PowerModel)power.ClonePreservingMutability(), power.Amount))
            .ToList();
        foreach ((PowerModel power, int amount) in debuffs.ToList())
        {
            if (power is not ITemporaryPower temporary)
                continue;
            int index = debuffs.FindIndex(entry => entry.Power.Id == temporary.InternallyAppliedPower.Id);
            if (index >= 0)
                debuffs[index] = (debuffs[index].Power, debuffs[index].Amount + amount);
        }

        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();

        foreach (Creature enemy in CombatState!.HittableEnemies.ToList())
        {
            if (enemy == cardPlay.Target)
                continue;
            foreach ((PowerModel power, int amount) in debuffs)
            {
                if (amount == 0)
                    continue;
                if (PowerCmd.FindExistingInstanceForStacking(power, enemy, power.Applier) is { } existing)
                {
                    await PowerCmd.ModifyAmount(CombatState, existing, amount, power.Applier, this);
                    continue;
                }

                await PowerCmd.Apply(CombatState, (PowerModel)power.ClonePreservingMutability(), enemy, amount,
                    power.Applier, this);
            }
        }
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
