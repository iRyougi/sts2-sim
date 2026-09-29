using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Rooms;

public sealed class Act1EventPoolTests
{
    private static readonly Type[] Expected =
    {
        typeof(AromaOfChaos),
        typeof(ByrdonisNest),
        typeof(DenseVegetation),
        typeof(JungleMazeAdventure),
        typeof(LuminousChoir),
        typeof(MorphicGrove),
        typeof(SapphireSeed),
        typeof(SunkenStatue),
        typeof(TabletOfTruth),
        typeof(UnrestSite),
        typeof(Wellspring),
        typeof(WhisperingHollow),
        typeof(WoodCarvings),
    };

    [Fact]
    public void All_ContainsExactlyTheThirteenAct1Events()
    {
        Assert.Equal(Expected, Act1EventPool.All);
        Assert.Equal(13, Act1EventPool.All.Distinct().Count());
    }
}
