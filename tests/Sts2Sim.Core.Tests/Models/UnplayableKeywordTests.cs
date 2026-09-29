using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class UnplayableKeywordTests : IDisposable
{
    public UnplayableKeywordTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate), typeof(DivineRight) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CanPlay_ReturnsUnplayableKeywordReason_WhenOtherwisePlayable()
    {
        Player player = CreatePlayerWithResources(energy: 1, stars: 1);
        CardModel card = CreateOwnedMutableCard<UnplayableTestCard>(player);

        bool canPlay = card.CanPlay(out UnplayableReason reason);

        Assert.False(canPlay);
        Assert.Equal(UnplayableReason.HasUnplayableKeyword, reason);
    }

    [Fact]
    public void CanPlay_ReturnsTrue_WhenOtherwiseIdenticalCardHasNoUnplayableKeyword()
    {
        Player player = CreatePlayerWithResources(energy: 1, stars: 1);
        CardModel card = CreateOwnedMutableCard<PlayableTestCard>(player);

        bool canPlay = card.CanPlay(out UnplayableReason reason);

        Assert.True(canPlay);
        Assert.Equal(UnplayableReason.None, reason);
    }

    [Fact]
    public void CanPlay_AccumulatesUnplayableAndResourceReasons_WhenResourcesAreInsufficient()
    {
        Player player = CreatePlayerWithResources(energy: 0, stars: 0);
        CardModel card = CreateOwnedMutableCard<UnplayableTestCard>(player);

        bool canPlay = card.CanPlay(out UnplayableReason reason);

        Assert.False(canPlay);
        Assert.Equal(
            UnplayableReason.HasUnplayableKeyword |
            UnplayableReason.EnergyCostTooHigh |
            UnplayableReason.StarCostTooHigh,
            reason);
    }

    [Fact]
    public void MutableClone_PreservesCanonicalUnplayableKeyword_AfterOwnerAssignment()
    {
        Player player = CreatePlayerWithResources(energy: 1, stars: 1);
        CardModel card = CreateOwnedMutableCard<UnplayableTestCard>(player);

        Assert.True(card.HasKeyword(CardKeyword.Unplayable));
        Assert.False(card.CanPlay(out UnplayableReason reason));
        Assert.Equal(UnplayableReason.HasUnplayableKeyword, reason);
    }

    private static Player CreatePlayerWithResources(int energy, int stars)
    {
        var runState = new RunState("unplayable-keyword", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.ResetCombatState();
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        player.PlayerCombatState!.Energy = energy;
        player.PlayerCombatState.GainStars(stars);
        return player;
    }

    private static TCard CreateOwnedMutableCard<TCard>(Player player)
        where TCard : CardModel, new()
    {
        var card = (TCard)new TCard().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private abstract class TestCard : CardModel
    {
        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Token;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 1;

        protected override int CanonicalStarCost => 1;
    }

    private sealed class UnplayableTestCard : TestCard
    {
        protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Unplayable };
    }

    private sealed class PlayableTestCard : TestCard
    {
    }
}
