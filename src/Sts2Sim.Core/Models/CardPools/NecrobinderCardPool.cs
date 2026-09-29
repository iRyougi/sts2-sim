using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.CardPools;

/// <summary>The v0.111.0 Necrobinder card pool in native GenerateAllCards order.</summary>
public sealed class NecrobinderCardPool : CardPoolModel
{
    public const string Necrobinder2EpochId = "NECROBINDER2_EPOCH";
    public const string Necrobinder5EpochId = "NECROBINDER5_EPOCH";
    public const string Necrobinder7EpochId = "NECROBINDER7_EPOCH";

    private static readonly Type[] OfficialCardTypes =
    [
        typeof(Afterlife), typeof(BansheesCry), typeof(BlightStrike), typeof(Bodyguard),
        typeof(BoneShards), typeof(BorrowedTime), typeof(Bury), typeof(Cacophony),
        typeof(Calcify), typeof(CallOfTheVoid), typeof(CaptureSpirit), typeof(Cleanse),
        typeof(Countdown), typeof(DanseMacabre), typeof(DeathMarch), typeof(Deathbringer),
        typeof(DeathsDoor), typeof(Debilitate), typeof(DefendNecrobinder), typeof(Defile),
        typeof(Defy), typeof(Delay), typeof(Demesne), typeof(DevourLife),
        typeof(Dirge), typeof(DrainPower), typeof(Dredge), typeof(Eidolon),
        typeof(EndOfDays), typeof(EnfeeblingTouch), typeof(Eradicate), typeof(Fear),
        typeof(Fetch), typeof(Flatten), typeof(ForbiddenGrimoire), typeof(Friendship),
        typeof(GlimpseBeyond), typeof(GraveWarden), typeof(Graveblast), typeof(Hang),
        typeof(Haunt), typeof(HighFive), typeof(Invoke), typeof(LegionOfBone),
        typeof(Lethality), typeof(Melancholy), typeof(Misery), typeof(NecroMastery),
        typeof(NegativePulse), typeof(Neurosurge), typeof(NoEscape), typeof(Oblivion),
        typeof(Pagestorm), typeof(Parse), typeof(Poke), typeof(Protector),
        typeof(PullAggro), typeof(PullFromBelow), typeof(Putrefy), typeof(Rattle),
        typeof(Reanimate), typeof(Reap), typeof(ReaperForm), typeof(Reave),
        typeof(RightHandHand), typeof(Sacrifice), typeof(Scourge), typeof(SculptingStrike),
        typeof(Seance), typeof(SentryMode), typeof(Severance), typeof(SharedFate),
        typeof(Shroud), typeof(SicEm), typeof(SleightOfFlesh), typeof(Snap),
        typeof(SoulStorm), typeof(Soulbound), typeof(Sow), typeof(SpiritOfAsh),
        typeof(Spur), typeof(Squeeze), typeof(StrikeNecrobinder), typeof(TheScythe),
        typeof(TimesUp), typeof(Transfigure), typeof(Undeath), typeof(Underworld),
        typeof(Unleash), typeof(Veilpiercer), typeof(Wisp),
    ];

    private static readonly Type[] Necrobinder2Cards =
        [typeof(Scourge), typeof(Oblivion), typeof(Countdown)];
    private static readonly Type[] Necrobinder5Cards =
        [typeof(Afterlife), typeof(Sacrifice), typeof(Calcify)];
    private static readonly Type[] Necrobinder7Cards =
        [typeof(SculptingStrike), typeof(Veilpiercer), typeof(BansheesCry)];

    public override IReadOnlyList<CardModel> AllCards => OfficialCardTypes
        .Select(type => (CardModel)ModelDb.Get(type)).ToArray();

    protected override IEnumerable<CardModel> FilterThroughEpochs(
        PlayerUnlockState unlockState,
        IEnumerable<CardModel> cards)
    {
        HashSet<Type> excluded = [];
        if (!unlockState.IsEpochRevealed(Necrobinder2EpochId))
            excluded.UnionWith(Necrobinder2Cards);
        if (!unlockState.IsEpochRevealed(Necrobinder5EpochId))
            excluded.UnionWith(Necrobinder5Cards);
        if (!unlockState.IsEpochRevealed(Necrobinder7EpochId))
            excluded.UnionWith(Necrobinder7Cards);
        return cards.Where(card => !excluded.Contains(card.GetType()));
    }
}
