using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Monsters;

[Collection("ModelDb")]
public sealed class WrigglerTests : IDisposable
{
    public WrigglerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Wriggler),
            typeof(Infection),
            typeof(AscendersBane),
            typeof(StrengthPower),
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CanonicalHpUsesSourceNonAscensionRangeAndMaximum()
    {
        Wriggler wriggler = ModelDb.Monster<Wriggler>();

        Assert.Equal(17, wriggler.MinInitialHp);
        Assert.Equal(21, wriggler.MaxInitialHp);
    }

    [Fact]
    public async Task ToughEnemies_UsesAuthoritativeHpRange()
    {
        (_, _, Wriggler wriggler) = await EnterWrigglerAtSlotAsync("wriggler1", ascension: (int)AscensionLevel.ToughEnemies);

        Assert.Equal(18, wriggler.MinInitialHp);
        Assert.Equal(22, wriggler.MaxInitialHp);
        Assert.InRange(wriggler.Creature.MaxHp, 18, 22);
    }

    [Fact]
    public async Task DeadlyEnemies_BiteDealsSevenDamage()
    {
        (Player player, _, Wriggler wriggler) = await EnterWrigglerAtSlotAsync("wriggler1", ascension: (int)AscensionLevel.DeadlyEnemies);
        int hpBefore = player.Creature.CurrentHp;

        await wriggler.PerformMove();

        Assert.Equal(hpBefore - 7, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task StateMachine_UsesAuthoritativeConditionalOpeningAndMoveIds()
    {
        (_, _, Wriggler wriggler) = await EnterWrigglerAtSlotAsync("wriggler1");
        MonsterMoveStateMachine machine = wriggler.MoveStateMachine!;

        Assert.IsType<ConditionalBranchState>(machine.States["INIT_MOVE"]);
        Assert.Contains("NASTY_BITE_MOVE", machine.States.Keys);
        Assert.Contains("WRIGGLE_MOVE", machine.States.Keys);
        Assert.Equal("NASTY_BITE_MOVE", wriggler.NextMove!.StateId);
    }

    [Fact]
    public async Task FourDenseVegetationSlots_AlternateBiteAndWriggleBySlot()
    {
        (_, _, CombatRoom room) = await EnterFourWrigglers();

        Assert.Equal(new uint?[] { 1, 2, 3, 4 }, room.Engine.State.Enemies.Select(enemy => enemy.CombatId));
        Assert.Equal(
            new[] { "NASTY_BITE_MOVE", "WRIGGLE_MOVE", "NASTY_BITE_MOVE", "WRIGGLE_MOVE" },
            room.Engine.State.Enemies.Select(enemy => enemy.Monster!.NextMove!.StateId));
        Assert.Equal(
            new[] { IntentType.Buff, IntentType.StatusCard },
            room.Engine.State.Enemies[1].Monster!.NextMove!.Intents.Select(intent => intent.IntentType));
    }

    [Fact]
    public async Task BiteDealsSixDamageThenAlternatesToWriggle()
    {
        (_, Player player, CombatRoom room) = await EnterFourWrigglers();
        Wriggler wriggler = Assert.IsType<Wriggler>(room.Engine.State.Enemies[0].Monster);
        int hpBefore = player.Creature.CurrentHp;

        await wriggler.PerformMove();
        wriggler.RollMove(room.Engine.State.Allies);

        Assert.Equal(hpBefore - 6, player.Creature.CurrentHp);
        Assert.Equal("WRIGGLE_MOVE", wriggler.NextMove!.StateId);
    }

    [Fact]
    public async Task WriggleAddsTwoStrengthAndOneInfectionToEachPlayerDiscard_ThenAlternatesToBite()
    {
        (_, Player player, CombatRoom room) = await EnterFourWrigglers();
        Wriggler wriggler = Assert.IsType<Wriggler>(room.Engine.State.Enemies[1].Monster);

        await wriggler.PerformMove();
        wriggler.RollMove(room.Engine.State.Allies);

        StrengthPower strength = Assert.IsType<StrengthPower>(wriggler.Creature.GetPower<StrengthPower>());
        Assert.Equal(2, strength.Amount);
        Infection infection = Assert.Single(player.PlayerCombatState!.DiscardPile.Cards.OfType<Infection>());
        Assert.Same(player, infection.Owner);
        Assert.Same(player.PlayerCombatState.DiscardPile, infection.Pile);
        Assert.Equal("NASTY_BITE_MOVE", wriggler.NextMove!.StateId);
    }

    [Fact]
    public async Task WrigglerInSlotOneStartsWithBiteAndAlternatesToWriggle()
    {
        (Player player, _, Wriggler wriggler) = await EnterWrigglerAtSlotAsync("wriggler1");
        int hpBefore = player.Creature.CurrentHp;

        Assert.Equal("NASTY_BITE_MOVE", wriggler.NextMove!.StateId);

        await wriggler.PerformMove();
        wriggler.RollMove(new[] { player.Creature });

        Assert.Equal(hpBefore - 6, player.Creature.CurrentHp);
        Assert.Equal("WRIGGLE_MOVE", wriggler.NextMove!.StateId);
    }

    [Fact]
    public async Task WrigglerInSlotTwoStartsWithWriggleAndAppliesItsEffects()
    {
        (Player player, _, Wriggler wriggler) = await EnterWrigglerAtSlotAsync("wriggler2");

        Assert.Equal("WRIGGLE_MOVE", wriggler.NextMove!.StateId);

        await wriggler.PerformMove();
        wriggler.RollMove(new[] { player.Creature });

        Assert.Single(player.PlayerCombatState!.DiscardPile.Cards.OfType<Infection>());
        Assert.Equal(2, wriggler.Creature.GetPower<StrengthPower>()!.Amount);
        Assert.Equal("NASTY_BITE_MOVE", wriggler.NextMove!.StateId);
    }

    [Fact]
    public async Task WrigglerInSlotThreeStartsWithBiteAndAlternatesToWriggle()
    {
        (Player player, _, Wriggler wriggler) = await EnterWrigglerAtSlotAsync("wriggler3");
        int hpBefore = player.Creature.CurrentHp;

        Assert.Equal("NASTY_BITE_MOVE", wriggler.NextMove!.StateId);

        await wriggler.PerformMove();
        wriggler.RollMove(new[] { player.Creature });

        Assert.Equal(hpBefore - 6, player.Creature.CurrentHp);
        Assert.Equal("WRIGGLE_MOVE", wriggler.NextMove!.StateId);
    }

    [Fact]
    public async Task WrigglerInSlotFourStartsWithWriggleAndAppliesItsEffects()
    {
        (Player player, _, Wriggler wriggler) = await EnterWrigglerAtSlotAsync("wriggler4");

        Assert.Equal("WRIGGLE_MOVE", wriggler.NextMove!.StateId);

        await wriggler.PerformMove();
        wriggler.RollMove(new[] { player.Creature });

        Assert.Single(player.PlayerCombatState!.DiscardPile.Cards.OfType<Infection>());
        Assert.Equal(2, wriggler.Creature.GetPower<StrengthPower>()!.Amount);
        Assert.Equal("NASTY_BITE_MOVE", wriggler.NextMove!.StateId);
    }

    [Fact]
    public async Task StunnedWrigglerSkipsSpawnTurnBeforeTakingItsSlotBasedBranch()
    {
        (Player player, _, Wriggler wriggler) = await EnterWrigglerAtSlotAsync("wriggler2", startStunned: true);
        int hpBefore = player.Creature.CurrentHp;

        Assert.Equal("SPAWNED_MOVE", wriggler.NextMove!.StateId);
        Assert.IsType<StunIntent>(Assert.Single(wriggler.NextMove.Intents));

        await wriggler.PerformMove();
        wriggler.RollMove(new[] { player.Creature });

        Assert.Equal(hpBefore, player.Creature.CurrentHp);
        Assert.Empty(player.PlayerCombatState!.DiscardPile.Cards.OfType<Infection>());
        Assert.Null(wriggler.Creature.GetPower<StrengthPower>());
        Assert.Equal("WRIGGLE_MOVE", wriggler.NextMove!.StateId);

        await wriggler.PerformMove();
        wriggler.RollMove(new[] { player.Creature });

        Assert.Single(player.PlayerCombatState.DiscardPile.Cards.OfType<Infection>());
        Assert.Equal(2, wriggler.Creature.GetPower<StrengthPower>()!.Amount);
        Assert.Equal("NASTY_BITE_MOVE", wriggler.NextMove!.StateId);
    }

    [Fact]
    public async Task WrigglerWithoutNativeSlotHasNoOpeningBranch()
    {
        // Native INIT_MOVE branches only on wriggler1..4; ConditionalBranchState throws otherwise.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => EnterWrigglerAtSlotAsync("dense-vegetation"));
    }

    private static async Task<(Player Player, CombatRoom Room, Wriggler Wriggler)> EnterWrigglerAtSlotAsync(
        string slotName,
        bool startStunned = false,
        int ascension = 0)
    {
        var runState = new RunState("wriggler-slot", new Overgrowth(), ascension);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var wriggler = (Wriggler)ModelDb.Monster<Wriggler>().MutableClone();
        wriggler.StartStunned = startStunned;
        var room = new CombatRoom(
            () => new (MonsterModel Monster, string? SlotName)[] { (wriggler, slotName) },
            RoomType.Monster);
        runState.PushRoom(room);
        await room.Enter(runState);
        return (player, room, wriggler);
    }
    private static async Task<(RunState RunState, Player Player, CombatRoom Room)> EnterFourWrigglers()
    {
        var runState = new RunState("wriggler-four", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(CreateFourWrigglers, RoomType.Monster);
        runState.PushRoom(room);
        await room.Enter(runState);
        return (runState, player, room);
    }

    // Native DenseVegetationEventEncounter slots.
    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateFourWrigglers() =>
        Enumerable.Range(1, 4)
            .Select(index => ((MonsterModel)ModelDb.Monster<Wriggler>().MutableClone(), (string?)$"wriggler{index}"))
            .ToList()
            .AsReadOnly();
}
