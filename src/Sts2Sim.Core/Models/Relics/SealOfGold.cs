using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SealOfGold : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    // #1046：原版挂在 AfterSideTurnStart（CombatManager.StartTurn 在 SetupPlayerTurn —— 重置能量、抽牌、
    // AfterPlayerTurnStart —— 之后才派发），不是 AfterEnergyReset；先 PlayerCmd.GainEnergy 再 LoseGold。
    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature) && Owner.Gold >= 3)
        {
            Flash();
            await PlayerCmd.GainEnergy(1m, Owner);
            await PlayerCmd.LoseGold(3m, Owner, GoldLossType.Lost);
        }
    }
}
