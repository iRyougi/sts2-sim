using Sts2Sim.Core.Models.Events;

namespace Sts2Sim.Core.Rooms;

/// <summary>Cross-act events in canonical ModelDb.AllSharedEvents order.</summary>
public static class SharedEventPool
{
    public static IReadOnlyList<Type> All { get; } = Array.AsReadOnly(new[]
    {
        typeof(BrainLeech), typeof(CrystalSphere), typeof(DollRoom), typeof(FakeMerchant),
        typeof(PotionCourier), typeof(RanwidTheElder), typeof(RelicTrader), typeof(RoomFullOfCheese),
        typeof(SelfHelpBook), typeof(SlipperyBridge), typeof(StoneOfAllTime), typeof(Symbiote),
        typeof(TeaMaster), typeof(TheFutureOfPotions), typeof(TheLegendsWereTrue), typeof(ThisOrThat),
        typeof(WarHistorianRepy), typeof(WelcomeToWongos),
    });
}
