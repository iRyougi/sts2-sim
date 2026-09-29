using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Odds;

public class UnknownMapPointOdds : AbstractOdds
{
    public const float baseMonsterOdds = 0.1f;

    public const float baseEliteOdds = -1f;

    public const float baseTreasureOdds = 0.02f;

    public const float baseShopOdds = 0.03f;

    private readonly IOddsHooks _hooks;

    /// <summary>累积概率轮盘与赔率更新的遍历顺序。偏离 #64：真实游戏遍历 Dictionary（顺序依赖
    /// CLR "插入序即枚举序"的实现细节，语言规范并未承诺），这里改为显式数组固定同一顺序
    /// （与下方两个字典的初始化插入序逐项一致），当前行为逐位不变，但确定性不再依赖运行时实现
    /// 细节。Plan06 若给 ?房间 扩充新房型，必须同步扩充本数组（只调 <see cref="SetBaseOdds"/>
    /// 不加进这里的房型不会参与 Roll——这继承了真实游戏的既有行为）。</summary>
    private static readonly RoomType[] _rollOrder =
    {
        RoomType.Monster,
        RoomType.Elite,
        RoomType.Treasure,
        RoomType.Shop,
    };

    private readonly Dictionary<RoomType, float> _baseOdds = new()
    {
        [RoomType.Monster] = baseMonsterOdds,
        [RoomType.Elite] = baseEliteOdds,
        [RoomType.Treasure] = baseTreasureOdds,
        [RoomType.Shop] = baseShopOdds,
    };

    private readonly Dictionary<RoomType, float> _nonEventOdds = new()
    {
        [RoomType.Monster] = baseMonsterOdds,
        [RoomType.Elite] = baseEliteOdds,
        [RoomType.Treasure] = baseTreasureOdds,
        [RoomType.Shop] = baseShopOdds,
    };

    public float MonsterOdds
    {
        get => _nonEventOdds[RoomType.Monster];
        set => _nonEventOdds[RoomType.Monster] = value;
    }

    public float EliteOdds
    {
        get => _nonEventOdds[RoomType.Elite];
        set => _nonEventOdds[RoomType.Elite] = value;
    }

    public float TreasureOdds
    {
        get => _nonEventOdds[RoomType.Treasure];
        set => _nonEventOdds[RoomType.Treasure] = value;
    }

    public float ShopOdds
    {
        get => _nonEventOdds[RoomType.Shop];
        set => _nonEventOdds[RoomType.Shop] = value;
    }

    public float EventOdds => Math.Max(0f, 1f - _nonEventOdds.Values.Where(v => v > 0f).Sum());

    public UnknownMapPointOdds(Rng rng, IOddsHooks hooks)
        : base(0f, rng)
    {
        _hooks = hooks;
    }

    public void SetBaseOdds(RoomType roomType, float baseOdds)
    {
        _baseOdds[roomType] = baseOdds;
    }

    /// <summary>偏离 #327：不移植上游 <c>Roll</c> 开头的首局新手引导分支
    /// （首局新手引导会把前两个 ? 房间固定为 Event、第三个固定为 Monster）。
    ///
    /// <c>NumberOfRuns</c> 是存档里的跨局累计游玩次数（<c>SerializableProgress</c> /
    /// <c>SerializableUnlockState</c>）。本项目既定假设是永远以全成就全解锁的完美存档运行，
    /// 该值恒非 0，此分支恒不可达——与 #322 同族。
    ///
    /// 这里**整个删掉**而不是留一个恒为假的开关，是因为留着就是留陷阱：
    /// 原先 <c>RoomFactory</c> 硬编码 <c>NumberOfRuns: 0</c>，让恒不可达的分支变成了恒执行，
    /// 而三次提前 return 还绕过了 <see cref="_rng"/>，使 RNG 流从第一个 ? 房间起就与真机错位。
    /// 没有任何编译或测试会报——测试当时全部传老玩家值，只有生产传 0。</summary>
    public RoomType Roll(IEnumerable<RoomType> blacklist) => Roll(blacklist, _rng);

    public RoomType Roll(IEnumerable<RoomType> blacklist, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        IReadOnlySet<RoomType> roomTypes = _nonEventOdds.Keys.Append(RoomType.Event).Except(blacklist).ToHashSet();
        roomTypes = _hooks.ModifyUnknownMapPointRoomTypes(roomTypes);
        RoomType result = roomTypes.Contains(RoomType.Event) ? RoomType.Event : roomTypes.Order().First();
        float roll = rng.NextFloat();
        float cumulative = 0f;
        foreach (RoomType roomType in _rollOrder)
        {
            float odds = _nonEventOdds[roomType];
            if (roomTypes.Contains(roomType) && !(odds < 0f))
            {
                cumulative += odds;
                if (roll <= cumulative)
                {
                    result = roomType;
                    break;
                }
            }
        }
        foreach (RoomType roomType in _rollOrder)
        {
            float baseOdds = _baseOdds[roomType];
            if (result == roomType)
            {
                _nonEventOdds[roomType] = baseOdds;
            }
            else if (roomTypes.Contains(roomType))
            {
                float increase = _hooks.ModifyOddsIncreaseForUnrolledRoomType(roomType, baseOdds);
                _nonEventOdds[roomType] += increase;
            }
        }
        return result;
    }

    public void ResetToBase()
    {
        foreach (RoomType roomType in _rollOrder)
        {
            _nonEventOdds[roomType] = _baseOdds[roomType];
        }
    }
}
