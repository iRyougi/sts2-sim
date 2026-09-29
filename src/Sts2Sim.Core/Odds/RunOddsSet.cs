using Sts2Sim.Core.Random;
using Sts2Sim.Core.Saves;

namespace Sts2Sim.Core.Odds;

public class RunOddsSet
{
    public UnknownMapPointOdds UnknownMapPoint { get; private init; } = null!;

    private RunOddsSet()
    {
    }

    public RunOddsSet(Rng unknownMapPointRng, IOddsHooks hooks)
    {
        UnknownMapPoint = new UnknownMapPointOdds(unknownMapPointRng, hooks);
    }

    public SerializableRunOddsSet ToSerializable()
    {
        return new SerializableRunOddsSet
        {
            UnknownMapPointMonsterOddsValue = UnknownMapPoint.MonsterOdds,
            UnknownMapPointEliteOddsValue = UnknownMapPoint.EliteOdds,
            UnknownMapPointTreasureOddsValue = UnknownMapPoint.TreasureOdds,
            UnknownMapPointShopOddsValue = UnknownMapPoint.ShopOdds,
        };
    }

    public static RunOddsSet FromSerializable(SerializableRunOddsSet save, Rng unknownMapPointRng, IOddsHooks hooks)
    {
        return new RunOddsSet
        {
            UnknownMapPoint = new UnknownMapPointOdds(unknownMapPointRng, hooks)
            {
                MonsterOdds = save.UnknownMapPointMonsterOddsValue,
                EliteOdds = save.UnknownMapPointEliteOddsValue,
                TreasureOdds = save.UnknownMapPointTreasureOddsValue,
                ShopOdds = save.UnknownMapPointShopOddsValue,
            },
        };
    }
}
