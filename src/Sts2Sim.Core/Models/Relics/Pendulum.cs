using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>原版 <c>Pendulum</c>（v0.111.0）：每次抽手牌前 TurnsSeen = (TurnsSeen + 1) % 3，归零的那一回合手牌多抽 1 张
/// （ModifyHandDraw，算在正常抽牌里）。原来的移植在回合开始后另抽 1 张（非手牌抽牌），会被提琴之类禁止回合内抽牌的效果挡掉，
/// 抽牌时机也比原版晚。计数是存档属性 TurnsSeen（跨战斗累计，移植快照映射到 _turnCounter）。</summary>
public sealed class Pendulum : RelicModel
{
    private const int Turns = 3;
    private int _turnCounter;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task BeforeHandDraw(Player player)
    {
        if (player == Owner)
        {
            _turnCounter = (_turnCounter + 1) % Turns;
        }
        return Task.CompletedTask;
    }

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        player == Owner && _turnCounter == 0 ? count + 1m : count;

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_turnCounter);
    }
}
