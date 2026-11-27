using System.Collections.ObjectModel;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.CardPools;

/// <summary>v0.114.0（a7b9a9c0）Fool 角色 91 张牌池与 Epoch 解锁过滤（原版 <c>FoolCardPool</c>）。
/// v0.114.0：Nock 后插入 Optimism；RapidShot 删除，TwinShot 占其原位。</summary>
public sealed class FoolCardPool : CardPoolModel
{
    /// <summary>无状态共享实例（原版 <c>ModelDb.CardPool&lt;FoolCardPool&gt;()</c>，BlotOutTheSky 用）。</summary>
    public static FoolCardPool Instance { get; } = new();

    public const string Fool2EpochId = "FOOL2_EPOCH";
    public const string Fool5EpochId = "FOOL5_EPOCH";
    public const string Fool7EpochId = "FOOL7_EPOCH";

    private static readonly Type[] OfficialCardTypes =
    [
        typeof(AllIsWell), typeof(Anguish), typeof(Archery), typeof(ArrowStab),
        typeof(BlossomShot), typeof(BlotOutTheSky), typeof(Bounce), typeof(BowSmack),
        typeof(Brace), typeof(BrightenUp), typeof(ChoBarbs), typeof(ChoChorus),
        typeof(ChoFirm), typeof(ChoKi), typeof(ChoLimbs), typeof(ChoReady),
        typeof(Coda), typeof(Compost), typeof(CoveringFire), typeof(DeepStretch),
        typeof(DefendFool), typeof(DislikeYou), typeof(DivineShot), typeof(Ecstasy),
        typeof(Encore), typeof(Ennui), typeof(Enrapture), typeof(Ensemble),
        typeof(EpicRetrieval), typeof(ExplosiveArrows), typeof(Florescence), typeof(Growth),
        typeof(HarmonicShield), typeof(HeavyShot), typeof(HiddenRoots), typeof(HobblingShot),
        typeof(Honing), typeof(Jest), typeof(KiChant), typeof(KiHop),
        typeof(KiRest), typeof(KiVerse), typeof(KiWhip), typeof(LockedIn),
        typeof(FoolishShot), typeof(LostLute), typeof(Lullaby), typeof(MelodicShot),
        typeof(Motif), typeof(MourningBlow), typeof(MythicForm), typeof(Nail),
        typeof(Nock), typeof(Optimism), typeof(PunchOfJoy), typeof(QuickShot), typeof(Quietus),
        typeof(Quiver), typeof(RainShot), typeof(TwinShot), typeof(Recall),
        typeof(RepeatShot), typeof(RhythmStrike), typeof(Rummage), typeof(SelfDeprecate),
        typeof(SerratedArrows), typeof(ShowOff), typeof(Simpleton), typeof(SkyShot),
        typeof(SongPath), typeof(SorrowfulStrike), typeof(SteadyCadence), typeof(StrikeFool),
        typeof(Timber), typeof(TrickShot), typeof(Tune), typeof(Uproot),
        typeof(ViolentChord), typeof(Vitriol), typeof(WahBlast), typeof(WahCho),
        typeof(WahChoKi), typeof(WahHammer), typeof(WahKi), typeof(WahSymphony),
        typeof(WahSweep), typeof(WahTouch), typeof(WhistlingArrows), typeof(Wrest),
        typeof(Yah), typeof(Yoga),
    ];

    private static readonly Type[] Fool2Cards =
        [typeof(ArrowStab), typeof(Honing), typeof(BlossomShot)];
    private static readonly Type[] Fool5Cards =
        [typeof(WahTouch), typeof(KiVerse), typeof(ChoLimbs)];
    private static readonly Type[] Fool7Cards =
        [typeof(SorrowfulStrike), typeof(Ennui), typeof(Growth)];

    private static int _poolGeneration;
    private static ReadOnlyCollection<CardModel>? _poolCache;

    public override IReadOnlyList<CardModel> AllCards =>
        ModelDb.ResolveStrictPool(OfficialCardTypes, ref _poolGeneration, ref _poolCache);

    protected override IEnumerable<CardModel> FilterThroughEpochs(
        PlayerUnlockState unlockState,
        IEnumerable<CardModel> cards)
    {
        HashSet<Type> excluded = [];
        if (!unlockState.IsEpochRevealed(Fool2EpochId))
            excluded.UnionWith(Fool2Cards);
        if (!unlockState.IsEpochRevealed(Fool5EpochId))
            excluded.UnionWith(Fool5Cards);
        if (!unlockState.IsEpochRevealed(Fool7EpochId))
            excluded.UnionWith(Fool7Cards);
        return excluded.Count == 0
            ? cards
            : cards.Where(card => !excluded.Contains(card.GetType()));
    }
}
