using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Events.Display;

/// <summary>
/// Display data of one event option, in the shape of the game's <c>EventOption</c> (偏离 #82: the simulator's
/// <see cref="EventOption"/> keeps only the key).
/// </summary>
/// <param name="TextKey">The game's <c>EventOption.TextKey</c>, <c>{EVENT}.pages.{PAGE}.options.{OPTION}</c>, or the
/// literal <c>PROCEED</c> for the Ancient / Relic Trader exit button.</param>
/// <param name="TextKeyIsListed">Whether <paramref name="TextKey"/> is one of the game's listed option text keys (false for
/// <c>PROCEED</c> and for the fallback rules).</param>
/// <param name="IsProceed">The game's <c>EventOption.IsProceed</c>.</param>
/// <param name="Relic">The game's <c>EventOption.Relic</c> (canonical model), or null.</param>
/// <param name="HoverObject">The first card, potion, or relic other than <paramref name="Relic"/> among the option's hover
/// tips (a <see cref="CardModel"/> copy at the shown upgrade level, a canonical <see cref="PotionModel"/> or
/// <see cref="RelicModel"/>, or for Slippery Bridge's Overcome the deck card itself), or null.</param>
public sealed record EventOptionDisplay(
    string TextKey,
    bool TextKeyIsListed,
    bool IsProceed,
    RelicModel? Relic,
    AbstractModel? HoverObject);
