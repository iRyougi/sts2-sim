using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Merchant;

file sealed class FallbackMerchantCharacter : CharacterModel
{
    public override int StartingHp => 75;
    public override int StartingGold => 99;
    public override IReadOnlyList<Type> StartingDeck => ModelDb.Character<Regent>().StartingDeck;
    public override IReadOnlyList<Type> StartingRelics => ModelDb.Character<Regent>().StartingRelics;
    public override Sts2Sim.Core.Models.CardPools.CardPoolModel CardPool => new FallbackMerchantPool();
}

file sealed class FallbackMerchantPool : Sts2Sim.Core.Models.CardPools.CardPoolModel
{
    public override IReadOnlyList<CardModel> AllCards => [ModelDb.Card<FallbackCommonAttack>(), ModelDb.Card<FallbackRareAttack>()];
}
file sealed class FallbackCommonAttack : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
}

file sealed class FallbackRareAttack : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
}

[Collection("ModelDb")]
public sealed class MerchantRarityFallbackTests : IDisposable
{
    public MerchantRarityFallbackTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(FallbackMerchantCharacter), typeof(StrikeRegent), typeof(DefendRegent),
            typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight), typeof(Circlet), typeof(FallbackCommonAttack), typeof(FallbackRareAttack),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CharacterMerchantSlot_FallsForwardFromMissingUncommonToRare()
    {
        string seed = Enumerable.Range(0, 1_000)
            .Select(index => $"merchant-fallback-{index}")
            .First(FirstShopRarityIsUncommon);

        MerchantInventory inventory = MerchantInventory.Generate(CreatePlayer(seed));

        Assert.IsType<FallbackRareAttack>(inventory.Cards[0].Card);
    }

    private static bool FirstShopRarityIsUncommon(string seed) =>
        CreatePlayer(seed).Odds.CardRarity.RollWithoutChangingFutureOdds(CardRarityOddsType.Shop) == CardRarity.Uncommon;

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<FallbackMerchantCharacter>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
