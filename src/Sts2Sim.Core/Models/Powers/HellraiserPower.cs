using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;

namespace Sts2Sim.Core.Models.Powers;

public sealed class HellraiserPower : PowerModel
{
    private const int InfiniteAutoPlayCap = 9;
    private int _infiniteAutoPlaysThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterCardDrawnEarly(CardModel card, bool fromHandDraw)
    {
        if (card.Owner.Creature != Owner || !card.Tags.Contains(CardTag.Strike))
        {
            return;
        }

        // The v0.111.0 source sets an infinite HP display in exactly these two paths.
        bool infiniteEnemies = Owner.CombatState!.HittableEnemies.All(HasInfiniteHpDisplay);
        if (infiniteEnemies)
        {
            if (_infiniteAutoPlaysThisTurn >= InfiniteAutoPlayCap)
            {
                _infiniteAutoPlaysThisTurn++;
                return;
            }
            _infiniteAutoPlaysThisTurn++;
        }
        else
        {
            _infiniteAutoPlaysThisTurn = 0;
        }

        await AutoPlayCmd.FromCards(Owner.CombatState, Owner.Player!, [card]);
    }

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            _infiniteAutoPlaysThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context) =>
        builder.Append(_infiniteAutoPlaysThisTurn);

    private static bool HasInfiniteHpDisplay(Creature enemy) =>
        enemy.Monster is WaterfallGiant && enemy.MaxHp == 999999999 ||
        enemy.GetPower<HardenedShellPower>() is { DisplayAmount: 0 };
}
