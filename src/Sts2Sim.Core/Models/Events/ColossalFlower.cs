using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events
{
    using Sts2Sim.Core.Models.Relics;

    public sealed class ColossalFlower : EventModel
    {
        private static readonly int[] Gold = { 35, 75, 135 };
        private static readonly int[] Damage = { 5, 6, 7 };
        private int _dig;

        public override bool IsAllowed(IRunState runState) =>
            runState.Players.All(player => player.Creature.CurrentHp >= 19);

        protected override IReadOnlyList<EventOption> GenerateInitialOptions() => DigOptions();

        private IReadOnlyList<EventOption> DigOptions() => new[]
        {
            new EventOption("EXTRACT_CURRENT_PRIZE", ExtractAsync),
            new EventOption("REACH_DEEPER", ReachAsync),
        };

        private async Task ReachAsync()
        {
            await DamageAsync(Damage[_dig]);
            _dig++;
            SetOptions(_dig < 2
                ? DigOptions()
                : new[]
                {
                    new EventOption("EXTRACT_INSTEAD", ExtractAsync),
                    new EventOption("POLLINOUS_CORE", ObtainCoreAsync),
                });
        }

        private async Task ExtractAsync()
        {
            await PlayerCmd.GainGold(Gold[_dig], Owner);
            Finish();
        }

        private async Task ObtainCoreAsync()
        {
            await DamageAsync(Damage[_dig]);
            await RelicCmd.Obtain(ModelDb.Relic<PollinousCore>(), Owner);
            Finish();
        }

        private Task DamageAsync(int amount) => CreatureCmd.Damage(
            RunState, Owner.Creature, amount, ValueProp.Unblockable | ValueProp.Unpowered);
    }
}
