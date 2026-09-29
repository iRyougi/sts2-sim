using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Melancholy</c>：获得 13（升级 17）格挡；这张牌在战斗牌堆里时，任何生物死亡（未被阻止移除）
/// 都让它本场战斗费用 -1。</summary>
public sealed class Melancholy : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool GainsBlock => true;

    protected override int CanonicalEnergyCost => 3;

    private decimal Block => IsUpgraded ? 17m : 13m;

    private const int Energy = 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block, Energy: Energy);

    protected override Task OnPlay(CardPlay cardPlay) =>
        CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);

    public override Task AfterDeath(Creature target, bool wasRemovalPrevented)
    {
        if (wasRemovalPrevented)
            return Task.CompletedTask;
        // 原版 PileType.IsCombatPile：Draw/Hand/Discard/Exhaust/Play。
        if (Pile?.Type is not (PileType.Draw or PileType.Hand or PileType.Discard or PileType.Exhaust or PileType.Play))
            return Task.CompletedTask;

        AddEnergyCostThisCombat(-Energy);
        return Task.CompletedTask;
    }
}
