using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class TrashToTreasurePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card.Type != CardType.Status || creator?.Creature != Owner) return;
        for (int i = 0; i < Amount; i++)
        {
            OrbModel orb = OrbModel.GetRandomOrb(Owner.Player!.RunState.Rng.CombatOrbGeneration).ToMutable();
            await OrbCmd.Channel(Owner.CombatState!, orb, Owner.Player);
        }
    }
}
