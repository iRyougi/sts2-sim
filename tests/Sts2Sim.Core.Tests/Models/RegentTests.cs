namespace Sts2Sim.Core.Tests.Models;

using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;

[Collection("ModelDb")]
public class RegentTests
{
    [Fact]
    public void Regent_HasGameGoldenValues()
    {
        try
        {
            ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight) });
            Regent regent = ModelDb.Character<Regent>();
            Assert.Equal(75, regent.StartingHp);
            Assert.Equal(99, regent.StartingGold);
            Assert.Equal(3, regent.MaxEnergy);
            Assert.Equal(0, regent.BaseOrbSlotCount);
            Assert.True(regent.IsPlayable);
            Assert.False(regent.ShouldReceiveCombatHooks);
        }
        finally
        {
            ModelDb.ResetForTests();
        }
    }

    [Fact]
    public void Regent_ModelId_IsCharacterRegent()
    {
        Assert.Equal(new ModelId("CHARACTER", "REGENT"), ModelDb.GetId<Regent>());
    }

    [Fact]
    public void CanonicalRegent_IsSharedInstance()
    {
        try
        {
            ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight) });
            Assert.Same(ModelDb.Character<Regent>(), ModelDb.Character<Regent>());
            Assert.True(ModelDb.Character<Regent>().IsCanonical);
        }
        finally
        {
            ModelDb.ResetForTests();
        }
    }

    [Fact]
    public void Regent_StartingDeckAndRelics_MatchRealGameContent()
    {
        try
        {
            ModelDb.Init(new[]
            {
                typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent),
                typeof(FallingStar), typeof(Venerate), typeof(DivineRight),
            });
            IReadOnlyList<Type> deck = ModelDb.Character<Regent>().StartingDeck;
            IReadOnlyList<Type> relics = ModelDb.Character<Regent>().StartingRelics;

            Assert.Equal(10, deck.Count);
            Assert.Equal(4, deck.Count(t => t == typeof(StrikeRegent)));
            Assert.Equal(4, deck.Count(t => t == typeof(DefendRegent)));
            Assert.Single(deck, t => t == typeof(FallingStar));
            Assert.Single(deck, t => t == typeof(Venerate));
            Assert.Equal(new[] { typeof(DivineRight) }, relics);
        }
        finally
        {
            ModelDb.ResetForTests();
        }
    }
}
