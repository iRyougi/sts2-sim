using System.Globalization;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class V01131SilentCardFactsTests : IDisposable
{
    public V01131SilentCardFactsTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("EXPERTISE", 0, 1, "Self", 0, 2, "RETAIN_HAND_POWER", 1)]
    [InlineData("EXPERTISE", 1, 1, "Self", 0, 3, "RETAIN_HAND_POWER", 1)]
    [InlineData("MEMENTO_MORI", 0, 3, "RandomEnemy", 19, 0, null, 0)]
    [InlineData("MEMENTO_MORI", 1, 3, "RandomEnemy", 23, 0, null, 0)]
    [InlineData("SPEEDSTER", 0, 2, "Self", 0, 0, "SPEEDSTER_POWER", 3)]
    [InlineData("SPEEDSTER", 1, 2, "Self", 0, 0, "SPEEDSTER_POWER", 3)]
    [InlineData("PHANTOM_BLADES", 0, 1, "Self", 0, 0, "PHANTOM_BLADES_POWER", 8)]
    [InlineData("PHANTOM_BLADES", 1, 1, "Self", 0, 0, "PHANTOM_BLADES_POWER", 11)]
    public void FourCards_ExposeV01131FaceAndProbeFacts(
        string id, int upgrade, int cost, string target, int damage, int cards, string? power, int amount)
    {
        CardModel card = (CardModel)ModelDb.GetById<CardModel>(new ModelId("CARD", id)).MutableClone();
        if (upgrade != 0) card.Upgrade();
        VisibleContent visible = VisibleContentProjection.ForCard(card);
        Assert.Equal(cost.ToString(CultureInfo.InvariantCulture), Assert.Single(visible.Values.Where(v => v.Name == "EnergyCost")).Value);
        Assert.Equal(target, Assert.Single(visible.Values.Where(v => v.Name == "TargetType")).Value);
        Assert.Equal(id == "MEMENTO_MORI", visible.Values.Any(v => v.Name == "Keyword" && v.Value == "Sly"));
        Assert.Equal(id == "SPEEDSTER" && upgrade != 0, visible.Values.Any(v => v.Name == "Keyword" && v.Value == "Innate"));
        Assert.Equal(damage, CardFaceBaseValues.Get(card, "Damage"));
        Assert.Equal(cards, CardFaceBaseValues.Get(card, "Cards"));
        if (id == "MEMENTO_MORI")
        {
            var provider = Assert.IsAssignableFrom<ICardDamageVariableProvider>(card);
            Assert.True(provider.TryGetThrashDamageVariable(out decimal liveDamage));
            Assert.Equal(damage, liveDamage);
        }
        CardEffectFacts facts = CardEffectProbe.Probe(id, upgrade);
        Assert.True(facts.Supported, facts.UnsupportedReason);
        Assert.Equal(damage, facts.Values.GetValueOrDefault("DamageDealt"));
        Assert.Equal(cards, facts.Values.GetValueOrDefault("CardsDrawn"));
        Assert.Equal(-cost, facts.Values.GetValueOrDefault("EnergyChange"));
        if (power is not null) Assert.Equal(amount, facts.Values.GetValueOrDefault($"Power.Self.{power}"));
    }
}
