using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>SoulboundPower</c>（仅限多人的 Soulbound 施加）：施加者生成一张 <see cref="Soul"/> 后，
/// 持有者把层数张 Soul 逐张随机洗入自己的抽牌堆。<see cref="_isAddingSoul"/> 只在本钩子内部为真，
/// 挡住自身生成的 Soul 再次触发（持有者即施加者时）。</summary>
public sealed class SoulboundPower : PowerModel
{
    private bool _isAddingSoul;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator is null || creator.Creature != Applier || card is not Soul || _isAddingSoul)
            return;

        Player owner = Owner.Player!;
        _isAddingSoul = true;
        try
        {
            List<Soul> souls = Soul.Create(owner, Amount);

            foreach (Soul soul in souls)
            {
                await CardPileCmd.Generate(Owner.CombatState!, soul, PileType.Draw, owner, CardPilePosition.Random);
            }
        }
        finally
        {
            _isAddingSoul = false;
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        context.AssertTransientEmpty(!_isAddingSoul, nameof(_isAddingSoul));
}
