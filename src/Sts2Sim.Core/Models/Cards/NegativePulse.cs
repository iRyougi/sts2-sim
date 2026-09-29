using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>NegativePulse</c>：获得 5（升级 6）格挡，给每个可命中的敌人施加 7（升级 11）
/// <see cref="DoomPower"/>。</summary>
public sealed class NegativePulse : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AllEnemies;

    public override bool GainsBlock => true;

    protected override int CanonicalEnergyCost => 1;

    private decimal Block => IsUpgraded ? 6m : 5m;

    private decimal Doom => IsUpgraded ? 11m : 7m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        foreach (Creature enemy in CombatState!.HittableEnemies.ToList())
            await PowerCmd.Apply<DoomPower>(CombatState, enemy, Doom, Owner.Creature, this);
    }
}
