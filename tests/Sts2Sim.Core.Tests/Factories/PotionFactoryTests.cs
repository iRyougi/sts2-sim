using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Tests.Factories;

[Collection("ModelDb")]
public sealed class PotionFactoryTests : IDisposable
{
    private sealed class TestCommonPotion : PotionModel
    {
        public override PotionRarity Rarity => PotionRarity.Common;

        public override PotionUsage Usage => PotionUsage.CombatOnly;

        public override TargetType TargetType => TargetType.Self;

        protected override Task OnUse(Creature? target) => Task.CompletedTask;
    }

    private sealed class TestRarePotion : PotionModel
    {
        public override PotionRarity Rarity => PotionRarity.Rare;

        public override PotionUsage Usage => PotionUsage.CombatOnly;

        public override TargetType TargetType => TargetType.Self;

        protected override Task OnUse(Creature? target) => Task.CompletedTask;
    }

    private sealed class TestCombatForbiddenUncommonPotion : PotionModel
    {
        public override PotionRarity Rarity => PotionRarity.Uncommon;
        public override PotionUsage Usage => PotionUsage.CombatOnly;
        public override TargetType TargetType => TargetType.Self;
        public override bool CanBeGeneratedInCombat => false;
        protected override Task OnUse(Creature? target) => Task.CompletedTask;
    }

    public PotionFactoryTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(TestCommonPotion), typeof(TestRarePotion), typeof(TestCombatForbiddenUncommonPotion) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CreateRandom_OnlyDrawsFromTheRequestedRarityBucket()
    {
        var rng = new Rng(1u);
        for (int i = 0; i < 50; i++)
        {
            PotionModel rare = Assert.IsType<TestRarePotion>(PotionFactory.CreateRandom(PotionFactory.Rarity.Rare, rng));
            PotionModel common = Assert.IsType<TestCommonPotion>(PotionFactory.CreateRandom(PotionFactory.Rarity.Common, rng));
        }
    }

    [Fact]
    public void CombatGeneration_ExcludesPotionMarkedIneligibleWhileGeneralFactoryKeepsItEligible()
    {
        PotionModel general = Assert.IsType<TestCombatForbiddenUncommonPotion>(
            PotionFactory.CreateRandom(PotionFactory.Rarity.Uncommon, new Rng(1u)));

        PotionModel? combat = PotionFactory.CreateRandomForCombat(
            PotionFactory.Rarity.Uncommon,
            new Rng(1u));

        Assert.Null(combat);
        Assert.False(general.CanBeGeneratedInCombat);
    }
}
