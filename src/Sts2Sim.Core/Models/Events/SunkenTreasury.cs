using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events;

public sealed class SunkenTreasury : EventModel
{
    private decimal _smallChestGold;
    private decimal _largeChestGold;

    protected override void CalculateVars()
    {
        _smallChestGold = 60m + Rng.NextInt(16) - 8;
        _largeChestGold = 333m + Rng.NextInt(61) - 30;
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("FIRST_CHEST", FirstChestAsync),
        new EventOption("SECOND_CHEST", SecondChestAsync),
    ];

    private async Task FirstChestAsync()
    {
        await PlayerCmd.GainGold(_smallChestGold, Owner);
        Finish();
    }

    private async Task SecondChestAsync()
    {
        await PlayerCmd.GainGold(_largeChestGold, Owner);
        await CardPileCmd.AddCursesToDeck([ModelDb.Card<Greed>()], Owner);
        Finish();
    }
}
