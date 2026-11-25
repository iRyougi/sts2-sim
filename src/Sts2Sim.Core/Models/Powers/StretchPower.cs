using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>下一张获格挡牌额外加格挡（原版 <c>MegaCrit.Sts2.Core.Models.Powers.StretchPower</c>）。</summary>
public sealed class StretchPower : PowerModel
{
    private CardPlay? _cardPlay;
    private int _amountWhenCardStarted;
    private bool _gaveBonusBlock;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player.Creature != Owner || !GainsBlock(cardPlay.Card) || _cardPlay is not null)
            return Task.CompletedTask;

        _cardPlay = cardPlay;
        _amountWhenCardStarted = Amount;
        return Task.CompletedTask;
    }

    public override decimal ModifyBlockAdditive(
        Creature target,
        decimal block,
        ValueProp props,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (cardSource is null || cardSource.Owner != Owner.Player)
            return 0m;
        if (_cardPlay is not null && !ReferenceEquals(_cardPlay, cardPlay))
            return 0m;
        if (!props.IsPoweredCardOrMonsterMoveBlock())
            return 0m;
        if (cardPlay is null)
            return GainsBlock(cardSource) ? Amount : 0m;
        if (_cardPlay is null || _cardPlay.Player != cardSource.Owner)
            return 0m;

        _gaveBonusBlock = true;
        // v0.114.0：加成量取出牌开始时记下的层数。
        return _amountWhenCardStarted;
    }

    private static bool GainsBlock(CardModel card) =>
        card.GainsBlock || card.Enchantments.Any(enchantment => enchantment.GainsBlock);

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay, _cardPlay))
        {
            _cardPlay = null;
            if (_gaveBonusBlock)
            {
                _gaveBonusBlock = false;
                await PowerCmd.ModifyAmount(Owner.CombatState!, this, -_amountWhenCardStarted, null, null);
            }
            _amountWhenCardStarted = 0;
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _cardPlay = null;
        _amountWhenCardStarted = 0;
        _gaveBonusBlock = false;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        context.AssertTransientEmpty(
            _cardPlay is null && _amountWhenCardStarted == 0 && !_gaveBonusBlock,
            $"{nameof(_cardPlay)}/{nameof(_amountWhenCardStarted)}/{nameof(_gaveBonusBlock)}");
    }
}
