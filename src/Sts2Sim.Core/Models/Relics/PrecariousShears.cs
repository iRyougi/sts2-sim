using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PrecariousShears : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        IReadOnlyList<CardModel> selected = (await CardSelectCmd.FromDeckForRemoval(Owner, 2, this));
        foreach (CardModel card in selected)
        {
            await CardPileCmd.RemoveFromDeck(Owner, card);
        }

        // 偏离 #166：权威源码精确使用 Unpowered；纠正计划早期摘要误写的 Unblockable。
        await CreatureCmd.Damage(
            Owner.RunState,
            Owner.Creature,
            16m,
            ValueProp.Unpowered);
    }
}
