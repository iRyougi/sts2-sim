using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.CardPools;

/// <summary>The v0.111.0 Ironclad card pool in native GenerateAllCards order.</summary>
public sealed class IroncladCardPool : CardPoolModel
{
    public const string Ironclad2EpochId = "IRONCLAD2_EPOCH";
    public const string Ironclad5EpochId = "IRONCLAD5_EPOCH";
    public const string Ironclad7EpochId = "IRONCLAD7_EPOCH";

    private static readonly Type[] OfficialCardTypes =
    [
        typeof(Aggression), typeof(Anger), typeof(Armaments), typeof(AshenStrike),
        typeof(Barricade), typeof(Bash), typeof(BattleTrance), typeof(Blaze),
        typeof(BloodWall), typeof(Bloodletting), typeof(Bludgeon), typeof(BodySlam),
        typeof(Brand), typeof(Break), typeof(Breakthrough), typeof(Bully),
        typeof(BurningPact), typeof(Cascade), typeof(Cinder), typeof(Colossus),
        typeof(Conflagration), typeof(Corruption), typeof(CrimsonMantle), typeof(Cruelty),
        typeof(DarkEmbrace), typeof(DefendIronclad), typeof(DemonForm), typeof(DemonicShield),
        typeof(Dismantle), typeof(Dominate), typeof(DrumOfBattle), typeof(EvilEye),
        typeof(ExpectAFight), typeof(Feed), typeof(FeelNoPain), typeof(FiendFire),
        typeof(FightMe), typeof(FlameBarrier), typeof(ForgottenRitual), typeof(Havoc),
        typeof(Headbutt), typeof(Hellraiser), typeof(Hemokinesis), typeof(HowlFromBeyond),
        typeof(Impervious), typeof(InfernalBlade), typeof(Inferno), typeof(Inflame),
        typeof(IronWave), typeof(Juggernaut), typeof(Juggling), typeof(Mangle),
        typeof(Midnight), typeof(MoltenFist), typeof(NotYet), typeof(Offering),
        typeof(OneTwoPunch), typeof(Outrage), typeof(PactsEnd), typeof(PerfectedStrike),
        typeof(Pillage), typeof(PommelStrike), typeof(PrimalForce), typeof(Pyre),
        typeof(Rage), typeof(Rampage), typeof(Rupture), typeof(SecondWind),
        typeof(SetupStrike), typeof(ShrugItOff), typeof(Spite), typeof(Stampede),
        typeof(Stoke), typeof(Stomp), typeof(StoneArmor), typeof(StrikeIronclad),
        typeof(SwordBoomerang), typeof(Tank), typeof(Taunt), typeof(TearAsunder),
        typeof(Thrash), typeof(Thunderclap), typeof(Tremble), typeof(TrueGrit),
        typeof(TwinStrike), typeof(Unmovable), typeof(Unrelenting), typeof(Uppercut),
        typeof(Vicious), typeof(Whirlwind),
    ];

    private static readonly Type[] Ironclad2Cards =
        [typeof(MoltenFist), typeof(Cruelty), typeof(Dominate)];
    private static readonly Type[] Ironclad5Cards =
        [typeof(Cinder), typeof(PactsEnd), typeof(DrumOfBattle)];
    private static readonly Type[] Ironclad7Cards =
        [typeof(BloodWall), typeof(TearAsunder), typeof(Inferno)];

    public override IReadOnlyList<CardModel> AllCards => OfficialCardTypes
        .Select(type => (CardModel)ModelDb.Get(type)).ToArray();

    protected override IEnumerable<CardModel> FilterThroughEpochs(
        PlayerUnlockState unlockState,
        IEnumerable<CardModel> cards)
    {
        HashSet<Type> excluded = [];
        if (!unlockState.IsEpochRevealed(Ironclad2EpochId))
            excluded.UnionWith(Ironclad2Cards);
        if (!unlockState.IsEpochRevealed(Ironclad5EpochId))
            excluded.UnionWith(Ironclad5Cards);
        if (!unlockState.IsEpochRevealed(Ironclad7EpochId))
            excluded.UnionWith(Ironclad7Cards);
        return cards.Where(card => !excluded.Contains(card.GetType()));
    }
}
