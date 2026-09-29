using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>BoneShards</c>：1 费 Osty 攻击。Osty 在场时由它对所有敌人造成 9（升级 12），主人获得 9（升级 12）格挡，
/// 随后若 Osty 仍存活则击杀它；Osty 不在时整张牌无效果。</summary>
public sealed class BoneShards : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AllEnemies;

    public override bool GainsBlock => true;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal OstyDamage => IsUpgraded ? 12m : 9m;

    private decimal Block => IsUpgraded ? 12m : 9m;

    // OstyDamage 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = OstyDamage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        if (Owner.IsOstyMissing)
            return;

        await DamageCmd.Attack(OstyDamage).FromOsty(Owner.Osty!, this, cardPlay)
            .TargetingAllOpponents(CombatState!).Execute();
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        if (Owner.IsOstyAlive)
            await CreatureCmd.Kill(Owner.Osty!);
    }
}
