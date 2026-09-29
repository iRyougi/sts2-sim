using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Exceptions;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class AscendersBaneTests : IDisposable
{
    public AscendersBaneTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void MetadataAndGenerationRestriction_MatchSource()
    {
        var card = (AscendersBane)ModelDb.Card<AscendersBane>().MutableClone();
        Assert.Equal(CardType.Curse, card.Type);
        Assert.Equal(CardRarity.Curse, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(-1, card.EnergyCost);
        Assert.Equal(0, card.MaxUpgradeLevel);
        Assert.False(card.CanBeGeneratedInCombat);
        Assert.False(card.CanBeGeneratedByModifiers);
        Assert.Equal(
            new[] { CardKeyword.Eternal, CardKeyword.Unplayable, CardKeyword.Ethereal },
            card.Keywords);
    }

    [Fact]
    public void FloorAddedToDeck_IsNullableMutationGuardedAndClonePreserved()
    {
        AscendersBane canonical = ModelDb.Card<AscendersBane>();

        Assert.Null(canonical.FloorAddedToDeck);
        Assert.Throws<CanonicalModelException>(() => canonical.FloorAddedToDeck = 1);

        var mutable = (AscendersBane)canonical.MutableClone();
        mutable.FloorAddedToDeck = 1;
        var cloned = (AscendersBane)mutable.MutableClone();

        Assert.Equal(1, mutable.FloorAddedToDeck);
        Assert.Equal(1, cloned.FloorAddedToDeck);
    }
}
