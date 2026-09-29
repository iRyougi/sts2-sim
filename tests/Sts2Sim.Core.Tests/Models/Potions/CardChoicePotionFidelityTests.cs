using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Potions;

[Collection("ModelDb")]
public sealed class CardChoicePotionFidelityTests : IDisposable
{
    public CardChoicePotionFidelityTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
        ModelDb.Init(new[] { typeof(RecipientCharacter), typeof(CreatorCharacter),
            typeof(CandidateA), typeof(CandidateB), typeof(CandidateC), typeof(CandidateD), typeof(MultiplayerCandidate),
            typeof(BasicCandidate), typeof(AncientCandidate), typeof(EventCandidate), typeof(ForbiddenCandidate) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    // Ordered pools and vectors from the v0.111.0 native pool declarations and card metadata.
    // Vectors use an independent descending Fisher–Yates loop, never the production generation helper.
    // See the 09-S1-22 execution report for the assembly/platform qualification and local evidence.
    private static string NativePool(string kind) => kind switch
    {
        "Attack" => "Assassinate Backstab DaggerSpray DaggerThrow Dash EchoingSlash Finisher Flechettes FlickFlack GrandFinale LeadingStrike MementoMori Murder Pinpoint PoisonedStab Pounce PreciseCut Predator Ricochet Skewer Slice Strangle SuckerPunch",
        "Skill" => "Acrobatics Adrenaline Anticipate Backflip BladeOfInk BladeDance Blur BouncingFlask BubbleBubble BulletTime Burst CalculatedGamble CloakAndDagger CorrosiveWave DeadlyPoison Deflect DodgeAndRoll EscapePlan Expertise Expose Sidestep HandTrick Haze HiddenDaggers KnifeTrap LegSweep Malaise Mirage Outbreak PiercingWail Prepared Reflex ShadowStep Shadowmeld Snakebite StormOfSteel Tactician Untouchable UpMySleeve",
        "Power" => "Abrasive Accelerant Accuracy Afterimage Envenom FanOfKnives Footwork InfiniteBlades MasterPlanner NoxiousFumes PhantomBlades SerpentForm Speedster ToolsOfTheTrade Tracking WellLaidPlans",
        "Colorless" => "Anointed Automation BeatDown Bolas Calamity Catastrophe DarkShackles Discovery DramaticEntrance Entropy Equilibrium EternalArmor Fasten Finesse Fisticuffs FlashOfSteel GoldAxe Impatience JackOfAllTrades Jackpot MasterOfStrategy Mayhem MindBlast Nostalgia Omnislice Panache PanicButton PrepTime Production Prolong Prowess Purity Rend Restlessness RollingBoulder Salvo Scrawl SecretTechnique SecretWeapon SeekerStrike Shockwave Splash Stratagem TheBomb TheGambit ThinkingAhead ThrummingHatchet UltimateDefend UltimateStrike Volley",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [Theory]
    [InlineData("Attack", "HSP7P798XTVC", "Dash DaggerThrow FlickFlack", 22, 2124664562)]
    [InlineData("Attack", "XEB2TX5C4XL6", "EchoingSlash Ricochet Skewer", 22, 2071359471)]
    [InlineData("Skill", "HSP7P798XTVC", "BladeDance BubbleBubble Untouchable", 38, 401551762)]
    [InlineData("Skill", "XEB2TX5C4XL6", "Malaise Expertise Prepared", 38, 1519507862)]
    [InlineData("Power", "HSP7P798XTVC", "WellLaidPlans InfiniteBlades ToolsOfTheTrade", 15, 1438873863)]
    [InlineData("Power", "XEB2TX5C4XL6", "Footwork ToolsOfTheTrade MasterPlanner", 15, 1855981314)]
    [InlineData("Colorless", "HSP7P798XTVC", "UltimateStrike Impatience UltimateDefend", 49, 714604651)]
    [InlineData("Colorless", "XEB2TX5C4XL6", "Production Omnislice UltimateStrike", 49, 16112059)]
    public async Task GenerateChoices_MatchesNativePoolOrderAndRng(
        string kind, string seed, string expectedChoices, int expectedDraws, int expectedNext)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        player.ResetCombatState();
        var combat = new CombatState(run);
        combat.AddPlayerCreature(player.Creature);
        var source = new ChoiceSource(cancel: false);
        combat.CardSelectionSource = source;
        CardPoolModel pool = kind == "Colorless" ? ColorlessCardPool.Instance : player.Character.CardPool;
        IEnumerable<CardModel> eligible = pool.GetUnlockedCards(player.UnlockState, false);
        if (kind != "Colorless") eligible = eligible.Where(card => card.Type.ToString() == kind);
        Assert.Equal(NativePool(kind).Split(' '), CardPoolFilters.ForCombatGeneration(eligible).Select(card => card.GetType().Name));
        var counters = Enum.GetValues<RunRngType>().ToDictionary(type => type, type => run.Rng.GetRng(type).Counter);
        PotionModel potion = (PotionModel)ModelDb.Get(typeof(PotionModel).Assembly.GetType($"Sts2Sim.Core.Models.Potions.{kind}Potion")!).MutableClone();
        potion.AssignOwner(player);

        await potion.UseInternal(player.Creature);

        Assert.Equal(expectedChoices.Split(' '), source.Candidates.Select(card => card.GetType().Name));
        Assert.Equal(expectedDraws, run.Rng.CombatCardGeneration.Counter);
        Assert.Equal(expectedNext, run.Rng.CombatCardGeneration.NextInt());
        foreach (var (type, counter) in counters)
            if (type != RunRngType.CombatCardGeneration) Assert.Equal(counter, run.Rng.GetRng(type).Counter);
        CardModel selected = Assert.Single(player.PlayerCombatState!.Hand.Cards);
        Assert.Same(source.Candidates[0], selected);
        Assert.All(source.Candidates, card => Assert.Same(player, card.Owner));
        Assert.True(selected.TemporaryFreeThisTurn);
        Assert.Equal(0, selected.EnergyCost);
        Assert.All(source.Candidates.Skip(1), card => Assert.Null(card.Pile));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(5, false)]
    [InlineData(5, true)]
    public async Task GenerateChoices_RespectsSmallPoolsAndSelectionOutcome(int size, bool cancel)
    {
        var run = new RunState("HSP7P798XTVC", new Overgrowth());
        Player creator = Player.CreateForNewRun(ModelDb.Character<CreatorCharacter>(), run);
        Player recipient = Player.CreateForNewRun(ModelDb.Character<RecipientCharacter>(), run);
        run.AddPlayer(creator);
        run.AddPlayer(recipient);
        var combat = new CombatState(run);
        foreach (Player player in run.Players)
        {
            player.ResetCombatState();
            combat.AddPlayerCreature(player.Creature);
        }
        var pool = (RecipientPool)recipient.Character.CardPool;
        pool.Size = size;
        var source = new ChoiceSource(cancel);
        combat.CardSelectionSource = source;
        // Source UnstableShuffle: descend from N-1 to 1, draw [0, i], then Take(3).
        // Work from the fixture's explicit eligible list, without calling pool filters or Shuffle.
        var expected = pool.Eligible.Take(size).ToArray();
        var oracleRng = run.Rng.CombatCardGeneration.CloneExact();
        for (int i = expected.Length - 1; i > 0; i--)
        {
            int j = oracleRng.NextInt(i + 1);
            (expected[i], expected[j]) = (expected[j], expected[i]);
        }
        PotionModel potion = (PotionModel)ModelDb.Potion<SkillPotion>().MutableClone();
        potion.AssignOwner(creator);

        await potion.UseInternal(recipient.Creature);

        Assert.Equal(expected.Take(3).Select(card => card.GetType()), source.Candidates.Select(card => card.GetType()));
        Assert.Equal(Math.Max(0, size - 1), run.Rng.CombatCardGeneration.Counter);
        Assert.Equal(oracleRng.NextInt(), run.Rng.CombatCardGeneration.NextInt());
        Assert.Empty(creator.PlayerCombatState!.Hand.Cards);
        Assert.All(source.Candidates, card => Assert.Same(recipient, card.Owner));
        if (cancel || size == 0)
        {
            Assert.Empty(recipient.PlayerCombatState!.Hand.Cards);
            Assert.All(source.Candidates, card => Assert.Null(card.Pile));
        }
        else
        {
            CardModel selected = Assert.Single(recipient.PlayerCombatState!.Hand.Cards);
            Assert.Same(source.Candidates[0], selected);
            Assert.True(selected.TemporaryFreeThisTurn);
            Assert.Equal(0, selected.EnergyCost);
        }
    }

    private sealed class ChoiceSource(bool cancel) : ICardSelectionDecisionSource
    {
        public IReadOnlyList<CardModel> Candidates { get; private set; } = Array.Empty<CardModel>();
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Assert.True(request.Cancelable);
            Assert.All(request.Candidates, card => Assert.Null(card.Pile));
            Candidates = request.Candidates;
            return Task.FromResult<IReadOnlyList<CardModel>>(cancel ? Array.Empty<CardModel>() : request.Candidates.Take(1).ToArray());
        }
    }
}

file sealed class CreatorCharacter : CharacterModel
{
    public override int StartingHp => 75;
    public override int StartingGold => 99;
}
file sealed class RecipientCharacter : CharacterModel
{
    public override int StartingHp => 75;
    public override int StartingGold => 99;
    public override CardPoolModel CardPool { get; } = new RecipientPool();
}
file sealed class RecipientPool : CardPoolModel
{
    public int Size { get; set; }
    public IReadOnlyList<CardModel> Eligible => [ModelDb.Card<CandidateA>(), ModelDb.Card<CandidateB>(),
        ModelDb.Card<CandidateC>(), ModelDb.Card<CandidateD>(), ModelDb.Card<MultiplayerCandidate>()];
    public override IReadOnlyList<CardModel> AllCards => Eligible.Take(Size)
        .Concat(Eligible.Take(Math.Min(Size, 1))) // duplicate canonical must not increase pool size
        .Concat(new CardModel[] { ModelDb.Card<BasicCandidate>(), ModelDb.Card<AncientCandidate>(),
            ModelDb.Card<EventCandidate>(), ModelDb.Card<ForbiddenCandidate>() }).ToArray();
}
file abstract class Candidate : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
}
file sealed class CandidateA : Candidate;
file sealed class CandidateB : Candidate;
file sealed class CandidateC : Candidate;
file sealed class CandidateD : Candidate;
file sealed class MultiplayerCandidate : Candidate { public override bool IsMultiplayerOnly => true; }
file sealed class BasicCandidate : Candidate { public override CardRarity Rarity => CardRarity.Basic; }
file sealed class AncientCandidate : Candidate { public override CardRarity Rarity => CardRarity.Ancient; }
file sealed class EventCandidate : Candidate { public override CardRarity Rarity => CardRarity.Event; }
file sealed class ForbiddenCandidate : Candidate { public override bool CanBeGeneratedInCombat => false; }
