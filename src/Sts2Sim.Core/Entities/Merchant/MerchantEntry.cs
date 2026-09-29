using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Entities.Merchant;

/// <summary>Base type for an entry on a merchant shelf.</summary>
public abstract class MerchantEntry
{
    private readonly int _basePrice;
    private readonly Player? _player;

    public int Price
    {
        get
        {
            decimal price = _basePrice;
            if (_player is not null)
            {
                price = Hook.ModifyMerchantPrice(
                    _player.RunState,
                    _player,
                    this,
                    price);
            }

            return (int)Math.Max(0m, price);
        }
    }

    public bool Purchased { get; private set; }

    protected MerchantEntry(int price, Player? player = null)
    {
        _basePrice = price;
        _player = player;
    }

    public void MarkPurchased() => Purchased = true;
}
