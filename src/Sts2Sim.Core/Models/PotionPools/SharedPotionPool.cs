using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Potions;

namespace Sts2Sim.Core.Models.PotionPools;

/// <summary>The official shared potion membership; unsupported content is omitted at ModelDb registration.</summary>
public sealed class SharedPotionPool : PotionPoolModel
{
    public static SharedPotionPool Instance { get; } = new();

    public const string Potion1EpochId = "POTION1_EPOCH";
    public const string Potion2EpochId = "POTION2_EPOCH";

    // Entity source SharedPotionPool: membership is explicit, not every registered potion.
    private static readonly Type[] PotionTypes =
    [
        typeof(AttackPotion), typeof(BeetleJuice), typeof(BlessingOfTheForge), typeof(BlockPotion),
        typeof(BottledPotential), typeof(Clarity), typeof(ColorlessPotion), typeof(CureAll),
        typeof(DexterityPotion), typeof(DistilledChaos), typeof(DropletOfPrecognition), typeof(Duplicator),
        typeof(EnergyPotion), typeof(EntropicBrew), typeof(ExplosiveAmpoule), typeof(FairyInABottle),
        typeof(FirePotion), typeof(FlexPotion), typeof(Fortifier), typeof(FruitJuice), typeof(FyshOil),
        typeof(GamblersBrew), typeof(GigantificationPotion), typeof(HeartOfIron), typeof(LiquidBronze),
        typeof(LiquidMemories), typeof(LuckyTonic), typeof(MazalethsGift), typeof(OrobicAcid),
        typeof(PotionOfBinding), typeof(PowderedDemise), typeof(PowerPotion), typeof(RadiantTincture),
        typeof(RegenPotion), typeof(ShacklingPotion), typeof(ShipInABottle), typeof(SkillPotion),
        typeof(SneckoOil), typeof(SpeedPotion), typeof(StableSerum), typeof(StrengthPotion),
        typeof(SwiftPotion), typeof(TouchOfInsanity), typeof(VulnerablePotion), typeof(WeakPotion),
    ];

    public override IReadOnlyList<PotionModel> AllPotions => PotionTypes
        .Where(ModelDb.Contains)
        .Select(type => (PotionModel)ModelDb.Get(type))
        .ToArray();

    public override IEnumerable<PotionModel> GetUnlockedPotions(PlayerUnlockState unlockState)
    {
        ArgumentNullException.ThrowIfNull(unlockState);
        bool potion1Unlocked = unlockState.IsEpochRevealed(Potion1EpochId);
        bool potion2Unlocked = unlockState.IsEpochRevealed(Potion2EpochId);
        return AllPotions.Where(potion =>
            (potion1Unlocked || potion is not BeetleJuice and not MazalethsGift and not DropletOfPrecognition) &&
            (potion2Unlocked || potion is not PowderedDemise and not ShipInABottle and not TouchOfInsanity));
    }
}
