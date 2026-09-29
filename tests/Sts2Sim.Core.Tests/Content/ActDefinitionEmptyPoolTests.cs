using Sts2Sim.Core.Content;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Content;

public sealed class ActDefinitionEmptyPoolTests
{
    [Theory]
    [InlineData(RoomType.Monster)]
    [InlineData(RoomType.Elite)]
    [InlineData(RoomType.Boss)]
    public void PickEncounter_WhenSelectedPoolIsEmpty_ThrowsClearInvalidOperationException(RoomType roomType)
    {
        var act = new EmptyPoolActDefinition(roomType);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => act.PickEncounter(roomType, new Rng(seed: 1)));

        Assert.Contains(roomType.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("pool", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class EmptyPoolActDefinition : ActDefinition
    {
        public override int Index => 0;
        public override IReadOnlyList<Type> EventPool => Array.Empty<Type>();
        public override IReadOnlyList<Type> AncientPool => [typeof(Sts2Sim.Core.Models.Events.Neow)];
        private static readonly IReadOnlyList<EncounterDefinition> NonEmptyPool = new[]
        {
            new EncounterDefinition(
                (Func<MonsterModel>)(() =>
                    throw new NotSupportedException("This selection test does not create monsters."))),
        };

        private readonly IReadOnlyList<EncounterDefinition> _monsterEncounters;
        private readonly IReadOnlyList<EncounterDefinition> _eliteEncounters;
        private readonly IReadOnlyList<EncounterDefinition> _bossEncounters;

        public override int BaseNumberOfRooms => 15;

        public override int NumberOfWeakEncounters => 3;

        protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => _monsterEncounters;

        protected override IReadOnlyList<EncounterDefinition> EliteEncounters => _eliteEncounters;

        protected override IReadOnlyList<EncounterDefinition> BossEncounters => _bossEncounters;

        public EmptyPoolActDefinition(RoomType emptyPool)
        {
            _monsterEncounters = emptyPool == RoomType.Monster
                ? Array.Empty<EncounterDefinition>()
                : NonEmptyPool;
            _eliteEncounters = emptyPool == RoomType.Elite
                ? Array.Empty<EncounterDefinition>()
                : NonEmptyPool;
            _bossEncounters = emptyPool == RoomType.Boss
                ? Array.Empty<EncounterDefinition>()
                : NonEmptyPool;
        }

        public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(12, 7);
    }
}
