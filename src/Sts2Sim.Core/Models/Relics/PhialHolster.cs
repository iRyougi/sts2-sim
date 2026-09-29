using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PhialHolster : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        Owner.GrowPotionSlots(1);
        var generatedIds = new HashSet<ModelId>();
        for (int i = 0; i < 2; i++)
        {
            // 偏离 #171 已销案（偏离 #306，2026-09-08）：权威 PhialHolster 走
            // PotionFactory.CreateRandomPotionsOutOfCombat，即角色池 ∪ 共享池；
            // 原实现从扁平的全部已注册药水里抽，与 Wellspring（偏离 #301）同源。
            // "本批次唯一"是本仓库既有的补充约束，保留。
            PotionModel? potion = PotionFactory.CreateRandomOutOfCombat(
                Owner,
                Owner.RunState.Rng.CombatPotionGeneration,
                candidate => !generatedIds.Contains(candidate.Id));
            if (potion is null)
            {
                continue;
            }

            generatedIds.Add(potion.Id);
            await PotionCmd.TryToProcure(potion, Owner);
        }
    }
}
