using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Events;

public sealed class CrystalSphereItem
{
    public CrystalSphereItemKind Kind { get; }
    public CardRarity CardRarity { get; }
    public PotionRarity PotionRarity { get; }
    public bool IsBigGold { get; }
    public int Width { get; }
    public int Height { get; }
    public (int X, int Y)? Position { get; internal set; }
    public bool IsRevealed { get; internal set; }
    internal int RevealSubscriptions { get; set; }

    internal CrystalSphereItem(CrystalSphereItemKind kind, CardRarity cardRarity = CardRarity.None,
        PotionRarity potionRarity = PotionRarity.Common, bool bigGold = false)
    {
        Kind = kind;
        CardRarity = cardRarity;
        PotionRarity = potionRarity;
        IsBigGold = bigGold;
        (Width, Height) = kind switch
        {
            CrystalSphereItemKind.Relic => (4, 4),
            CrystalSphereItemKind.Potion when potionRarity != PotionRarity.Rare => (1, 3),
            CrystalSphereItemKind.Gold => (bigGold ? 2 : 1, 1),
            _ => (2, 2),
        };
    }
}
