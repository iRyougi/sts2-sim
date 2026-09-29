using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 seed event. 偏离 #82/#148：省略选择 UI、附魔 VFX 与 DynamicVars 文案容器，
/// 在无头事件状态机中保留 Heal 9、玩家选择升级、首张附魔占位及空候选完成语义。</summary>
public sealed class SapphireSeed : EventModel
{
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("EAT", EatAsync),
        new EventOption("PLANT", PlantAsync),
    };

    private async Task EatAsync()
    {
        await CreatureCmd.Heal(Owner.Creature, 9m);
        CardModel? card = (await CardSelectCmd.FromDeckForUpgrade(Owner, 1, this)).FirstOrDefault();
        if (card is not null)
        {
            CardCmd.Upgrade(card);
        }

        Finish();
    }

    private async Task PlantAsync()
    {
        Sown sown = ModelDb.GetById<Sown>(ModelDb.GetId<Sown>());
        CardModel? card = Owner.Deck.Cards.FirstOrDefault(sown.CanEnchant);
        if (card is not null)
        {
            await CardCmd.Enchant<Sown>(card, 1m);
        }

        Finish();
    }
}
