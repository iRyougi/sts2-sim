using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Commands;

/// <summary>Fool 旋律状态查询命令（逐字移植 <c>MegaCrit.Sts2.Core.Commands.MelodyCmd</c>）。
/// 偏离 #106：无完整 CombatHistory 时以 <see cref="PlayerCombatState"/> 记本回合开始序与已完成出牌次数（不过滤自动出牌）。</summary>
public static class MelodyCmd
{
    public static bool PlayedWahThisTurn(Player player) => PlayedThisTurn(player, CardTag.Wah);

    public static bool PlayedChoThisTurn(Player player) => PlayedThisTurn(player, CardTag.Cho);

    public static bool PlayedKiThisTurn(Player player) => PlayedThisTurn(player, CardTag.Ki);

    public static bool PlayedThisTurn(Player player, CardTag cardTag)
    {
        PlayerCombatState? combatState = player.PlayerCombatState;
        return cardTag switch
        {
            CardTag.Wah => (combatState?.WahFinishedThisTurn ?? 0) > 0,
            CardTag.Cho => (combatState?.ChoFinishedThisTurn ?? 0) > 0,
            CardTag.Ki => (combatState?.KiFinishedThisTurn ?? 0) > 0,
            _ => throw new InvalidOperationException($"{cardTag} is not a valid song tag"),
        };
    }

    public static bool IsFirstPlayThisTurn(CardPlay cardPlay, CardTag cardTag)
    {
        PlayerCombatState? combatState = cardPlay.Player.PlayerCombatState;
        int firstOrdinal = cardTag switch
        {
            CardTag.Wah => combatState?.FirstWahPlayOrdinalThisTurn ?? 0,
            CardTag.Cho => combatState?.FirstChoPlayOrdinalThisTurn ?? 0,
            CardTag.Ki => combatState?.FirstKiPlayOrdinalThisTurn ?? 0,
            _ => throw new InvalidOperationException($"{cardTag} is not a valid song tag"),
        };
        return firstOrdinal > 0 && firstOrdinal == cardPlay.PlayOrdinal;
    }

    public static int MelodiesThisTurn(Player player) =>
        (PlayedWahThisTurn(player) ? 1 : 0) +
        (PlayedChoThisTurn(player) ? 1 : 0) +
        (PlayedKiThisTurn(player) ? 1 : 0);

    public static int MelodyCardsPlayedThisTurn(Player player) =>
        player.PlayerCombatState?.MelodyCardsFinishedThisTurn ?? 0;
}
