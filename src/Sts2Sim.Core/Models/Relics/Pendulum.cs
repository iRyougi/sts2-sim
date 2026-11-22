using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Pendulum : RelicModel
{
    private const int TurnsThreshold = 3; // DynamicVar "Turns"

    private int _turnCounter;

    public override RelicRarity Rarity => RelicRarity.Common;

    // 原版 Pendulum.BeforeHandDraw：TurnsSeen = (TurnsSeen + 1) % Turns，初值 0（新获得的遗物第 3、6… 次抽手牌时归零）；
    // 计数只在持有者抽手牌前推进，Status 在 TurnsSeen == Turns - 1 时为 Active。
    public override Task BeforeHandDraw(Player player)
    {
        if (player != Owner)
        {
            return Task.CompletedTask;
        }

        _turnCounter = (_turnCounter + 1) % TurnsThreshold;
        Status = _turnCounter == TurnsThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;
        return Task.CompletedTask;
    }

    // 原版 Pendulum.ModifyHandDraw：TurnsSeen == 0 时手牌抽牌数 + Cards(1)，算在正常手牌抽牌里。
    public override decimal ModifyHandDraw(Player player, decimal count) =>
        player == Owner && _turnCounter == 0 ? count + 1m : count;

    public override Task AfterCombatEnd()
    {
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_turnCounter);
    }
}
