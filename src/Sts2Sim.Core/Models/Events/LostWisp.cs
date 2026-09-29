using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events
{
    /// <summary>
    /// Source-disambiguated from <c>MegaCrit.Sts2.Core.Models.Events/LostWisp.cs</c>.
    /// The event RNG rolls 60 +/-15 exactly once before presenting CLAIM and SEARCH.
    /// </summary>
    public sealed class LostWisp : EventModel
    {
        private int _gold;

        protected override void CalculateVars() => _gold = 60 + Rng.NextInt(-15, 16);

        protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
        {
            new EventOption("CLAIM", ClaimAsync),
            new EventOption("SEARCH", SearchAsync),
        };

        private async Task ClaimAsync()
        {
            await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Decay>() }, Owner);
            await RelicCmd.Obtain(ModelDb.Relic<Sts2Sim.Core.Models.Relics.LostWisp>(), Owner);
            Finish();
        }

        private async Task SearchAsync()
        {
            await PlayerCmd.GainGold(_gold, Owner);
            Finish();
        }
    }
}
