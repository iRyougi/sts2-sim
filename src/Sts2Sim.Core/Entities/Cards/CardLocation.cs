using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Entities.Cards;

public record struct CardLocation(Player Player, PileType PileType, CardPilePosition Position);
