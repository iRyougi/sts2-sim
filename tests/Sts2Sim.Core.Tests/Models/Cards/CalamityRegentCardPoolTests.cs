using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class CalamityRegentCardPoolTests : IDisposable
{
    private static readonly Type[] OfficialRegentCardTypes =
    {
        typeof(Alignment), typeof(Arsenal), typeof(AstralPulse), typeof(BeatIntoShape),
        typeof(Begone), typeof(BigBang), typeof(BlackHole), typeof(Bombardment), typeof(Bulwark),
        typeof(BundleOfJoy), typeof(CelestialMight), typeof(Charge), typeof(ChildOfTheStars),
        typeof(CloakOfStars), typeof(CollisionCourse), typeof(Comet), typeof(Conqueror),
        typeof(Constellation), typeof(Convergence), typeof(CosmicIndifference), typeof(CrashLanding),
        typeof(CrescentSpear), typeof(CrushUnder), typeof(DecisionsDecisions), typeof(DefendRegent),
        typeof(Devastate), typeof(DyingStar), typeof(FallingStar), typeof(ForegoneConclusion),
        typeof(Furnace), typeof(GammaBlast), typeof(GatherLight), typeof(Genesis), typeof(Glimmer),
        typeof(Glitterstream), typeof(Glow), typeof(Guards), typeof(GuidingStar), typeof(HammerTime),
        typeof(HeavenlyDrill), typeof(Hegemony), typeof(HeirloomHammer), typeof(HiddenCache),
        typeof(IAmInvincible), typeof(KinglyKick), typeof(KinglyPunch), typeof(KnockoutBlow),
        typeof(KnowThyPlace), typeof(Largesse), typeof(LunarBlast), typeof(MakeItSo),
        typeof(ManifestAuthority), typeof(MeteorShower), typeof(MonarchsGaze), typeof(Monologue),
        typeof(NeutronAegis), typeof(Orbit), typeof(PaleBlueDot), typeof(Parry), typeof(ParticleWall),
        typeof(Patter), typeof(PhotonCut), typeof(Plot), typeof(PillarOfCreation), typeof(Prophesize),
        typeof(Quasar), typeof(Radiate), typeof(RefineBlade), typeof(Reflect), typeof(Resonance),
        typeof(RoyalGamble), typeof(Royalties), typeof(SeekingEdge), typeof(SevenStars),
        typeof(ShiningStrike), typeof(SolarStrike), typeof(SpectrumShift), typeof(SpoilsOfBattle),
        typeof(Stardust), typeof(StrikeRegent), typeof(SummonForth), typeof(Supermassive),
        typeof(SwordSage), typeof(Terraforming), typeof(TheSealedThrone), typeof(TheSmith),
        typeof(Tutor), typeof(Tyranny), typeof(Venerate), typeof(VoidForm), typeof(WroughtInWar),
    };

    private static readonly Type[] EpochLockedCardTypes =
    {
        typeof(SpoilsOfBattle), typeof(SwordSage), typeof(Furnace),
        typeof(Begone), typeof(Arsenal), typeof(Supermassive),
        typeof(Patter), typeof(HeavenlyDrill), typeof(LunarBlast),
    };

    public CalamityRegentCardPoolTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Append(typeof(CalamityPoolTestMonster)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void RegentCardPool_HasStableOfficialOrderAndFiltersEpochsAndMultiplayer()
    {
        Regent regent = ModelDb.Character<Regent>();

        Assert.Equal(OfficialRegentCardTypes, regent.CardPool.AllCards.Select(card => card.GetType()));

        var noEpochs = new PlayerUnlockState(
            Array.Empty<ModelId>(),
            Array.Empty<string>());
        Type[] expectedSinglePlayer = OfficialRegentCardTypes
            .Except(EpochLockedCardTypes)
            .Where(type => !ModelDb.GetById<CardModel>(ModelDb.GetId(type)).IsMultiplayerOnly)
            .ToArray();
        Assert.Equal(
            expectedSinglePlayer,
            regent.CardPool.GetUnlockedCards(noEpochs, isMultiplayer: false)
                .Select(card => card.GetType()));

        Type[] expectedMultiplayer = OfficialRegentCardTypes.Except(EpochLockedCardTypes).ToArray();
        Assert.Equal(
            expectedMultiplayer,
            regent.CardPool.GetUnlockedCards(noEpochs, isMultiplayer: true)
                .Select(card => card.GetType()));
        Assert.Contains(typeof(Constellation), expectedMultiplayer);
        Assert.Contains(typeof(HammerTime), expectedMultiplayer);
        Assert.Contains(typeof(Largesse), expectedMultiplayer);
        Assert.DoesNotContain(typeof(Constellation), expectedSinglePlayer);

        // 默认构造即"全部 epoch 已揭示"，无需再传清单（见 PlayerUnlockState 的说明）。
        var allEpochs = new PlayerUnlockState(Array.Empty<ModelId>());
        Assert.Equal(
            OfficialRegentCardTypes,
            regent.CardPool.GetUnlockedCards(allEpochs, isMultiplayer: true)
                .Select(card => card.GetType()));
    }

    [Fact]
    public async Task Calamity_UsesRealRegentUnlockedPoolAgainstAdversarialGlobalPoolRng()
    {
        var unlockState = new PlayerUnlockState(Array.Empty<ModelId>(), Array.Empty<string>());
        var runState = new RunState("calamity-real-regent-pool", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState, unlockState);
        runState.AddPlayer(player);
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<CalamityPoolTestMonster>().MutableClone());
        await room.Enter(runState);
        ClearHand(player);

        HashSet<Type> expectedAttackTypes = OfficialRegentCardTypes
            .Except(EpochLockedCardTypes)
            .Select(type => ModelDb.GetById<CardModel>(ModelDb.GetId(type)))
            .Where(card =>
                card.Type == CardType.Attack &&
                !card.IsMultiplayerOnly &&
                card.CanBeGeneratedInCombat &&
                card.Rarity is not CardRarity.Basic and not CardRarity.Ancient and not CardRarity.Event)
            .Select(card => card.GetType())
            .ToHashSet();
        var clonedState = room.Engine.State.Clone();
        Player clonedPlayer = Assert.Single(clonedState.Players);
        Assert.Same(unlockState, clonedPlayer.UnlockState);
        Assert.Equal(
            player.Character.CardPool.GetUnlockedCards(unlockState, isMultiplayer: false)
                .Select(card => card.GetType()),
            clonedPlayer.Character.CardPool.GetUnlockedCards(
                    clonedPlayer.UnlockState, isMultiplayer: false)
                .Select(card => card.GetType()));
        List<CardModel> brokenGlobalCandidates = CardPoolFilters.ForCombatGeneration(
                ModelDb.All<CardModel>().Where(card => !card.IsColorless && card.Type == CardType.Attack),
                isMultiplayer: false)
            .ToList();
        ulong adversarialSeed = Enumerable.Range(0, 10_000)
            .Select(value => (ulong)value)
            .First(seed =>
            {
                var rng = new Rng(seed);
                CardModel selected = rng.NextItem(brokenGlobalCandidates)!;
                return !expectedAttackTypes.Contains(selected.GetType());
            });
        runState.Rng.MockRng(RunRngType.CombatCardGeneration, adversarialSeed);

        await PowerCmd.Apply<CalamityPower>(
            room.Engine.State, player.Creature, 1m, player.Creature, null);
        var trigger = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        trigger.AssignOwner(player);
        CardPileCmd.Add(trigger, PileType.Hand);
        player.PlayerCombatState!.Energy = 3;

        await room.Engine.PlayCardAsync(player, trigger, room.Engine.State.Enemies.Single());

        CardModel generated = Assert.Single(player.PlayerCombatState.Hand.Cards);
        Assert.Contains(generated.GetType(), expectedAttackTypes);
        Assert.Same(player, generated.Owner);
    }

    private static void ClearHand(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToArray())
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
    }
}

file sealed class CalamityPoolTestMonster : MonsterModel
{
    public override int MinInitialHp => 500;
    public override int MaxInitialHp => 500;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("WAIT", _ => Task.CompletedTask, new SingleAttackIntent(0));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }
}
