using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>CaptureSpirit</c>：1 费技能，对目标造成 3（升级 4）点不可格挡的无力伤害，再把 3（升级 4）张
/// <see cref="Soul"/> 逐张随机洗入抽牌堆。灵魂先全部创建再逐张加入，与原版 <c>Soul.Create</c> +
/// <c>AddGeneratedCardsToCombat</c> 的顺序一致。</summary>
public sealed class CaptureSpirit : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    private decimal Damage => IsUpgraded ? 4m : 3m;

    private int Cards => IsUpgraded ? 4 : 3;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await CreatureCmd.Damage(CombatState!, [cardPlay.Target], Damage,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, Owner.Creature, this, cardPlay);

        List<Soul> souls = Soul.Create(Owner, Cards);

        foreach (Soul soul in souls)
            await CardPileCmd.Generate(CombatState!, soul, PileType.Draw, Owner, CardPilePosition.Random);
    }
}
