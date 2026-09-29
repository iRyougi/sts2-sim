using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Defy</c>：1 费虚无技能，获得 6（升级 9）格挡，再给目标施加 1 虚弱。</summary>
public sealed class Defy : CardModel, ICardChoiceBaseValueProvider
{
    private const decimal Weak = 1m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    public override bool GainsBlock => true;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    private decimal Block => IsUpgraded ? 9m : 6m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<WeakPower>(CombatState!, cardPlay.Target, Weak, Owner.Creature, this);
    }
}
