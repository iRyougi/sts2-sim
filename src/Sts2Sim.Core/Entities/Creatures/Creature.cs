using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Entities.Creatures;

/// <summary>
/// 战斗参与者外壳（玩家和怪物共用）。逐字移植自 v0.109（<c>MegaCrit.Sts2.Core.Entities.Creatures.Creature</c>）的
/// HP/格挡/powers 核心数学；Player/Monster 关联构造见 Task 4（<see cref="Player"/>）与 Task 12/13（<see cref="MonsterModel"/>）。
/// </summary>
public sealed class Creature
{
    private readonly List<PowerModel> _powers = new();
    private Player? _petOwner;

    public int Block { get; private set; }

    public int CurrentHp { get; private set; }

    public int CumulativeHpLost { get; private set; }

    public int MaxHp { get; private set; }

    public uint? CombatId { get; set; }

    public MonsterModel? Monster { get; private set; }

    public Player? Player { get; private set; }

    public Player? PetOwner
    {
        get => _petOwner;
        set
        {
            if (_petOwner is not null)
            {
                throw new InvalidOperationException($"Pet {this} already has an owner.");
            }
            _petOwner = value;
        }
    }

    public bool IsPet => PetOwner is not null;

    public IReadOnlyList<Creature> Pets =>
        Player?.PlayerCombatState?.Pets ?? Array.Empty<Creature>();

    public CombatSide Side { get; private set; }

    public string? SlotName { get; private set; }

    public ICombatState? CombatState { get; set; }

    public bool IsMonster => Monster != null;

    public bool IsPlayer => Player != null;

    public bool IsAlive => CurrentHp > 0;

    public bool IsDead => !IsAlive;

    /// <summary>A living in-combat creature that no active hook currently prevents effects from hitting.</summary>
    public bool IsHittable =>
        IsAlive && CombatState is { } combatState && Hook.ShouldAllowHitting(combatState, this);

    public bool IsSecondaryEnemy =>
        Side == CombatSide.Enemy && Powers.Any(power => power.OwnerIsSecondaryEnemy);

    public bool IsPrimaryEnemy => Side == CombatSide.Enemy && !IsSecondaryEnemy;

    public IReadOnlyList<PowerModel> Powers => _powers;

    public event Action<Creature>? Died;

    public Creature(MonsterModel monster, CombatSide side)
    {
        int minInitialHp = monster.MinInitialHp;
        int maxInitialHp = monster.MaxInitialHp;
        if (minInitialHp > maxInitialHp)
        {
            throw new InvalidOperationException(
                $"{monster.Id.Entry} has min HP {minInitialHp} greater than its max {maxInitialHp}!");
        }

        Monster = monster;
        Side = side;
        MaxHp = maxInitialHp;
        CurrentHp = MaxHp;
    }

    public Creature(Player player, int currentHp, int maxHp)
    {
        Player = player;
        Side = CombatSide.Player;
        MaxHp = maxHp;
        CurrentHp = currentHp;
    }

    /// <summary>仅供测试使用——不挂 Player/Monster,只验证 HP/格挡/powers 数学。`public`(非 `internal`):
    /// 测试项目是独立程序集,没有配置 InternalsVisibleTo,需要 `public` 才能跨程序集调用。</summary>
    public static Creature CreateStandaloneForTests(int currentHp, int maxHp)
    {
        return new Creature(currentHp, maxHp);
    }

    private Creature(int currentHp, int maxHp)
    {
        Side = CombatSide.Player;
        MaxHp = maxHp;
        CurrentHp = currentHp;
    }

    public void SetUniqueMonsterHpValue(IReadOnlyList<Creature> creaturesOnSide, Rng rng)
    {
        ArgumentNullException.ThrowIfNull(creaturesOnSide);
        ArgumentNullException.ThrowIfNull(rng);
        if (Monster is null)
        {
            throw new InvalidOperationException("Can't set unique monster HP value for a player.");
        }

        int min = Monster.MinInitialHp;
        int max = Monster.MaxInitialHp;
        if (min > max)
        {
            throw new InvalidOperationException(
                $"{Monster.Id.Entry} has min HP {min} greater than its max {max}!");
        }

        long rangeSize = checked((long)max - min + 1L);
        var usedHp = new SortedSet<int>();
        foreach (Creature other in creaturesOnSide)
        {
            if (!ReferenceEquals(other, this) && other.IsMonster && other.MaxHp >= min && other.MaxHp <= max)
            {
                usedHp.Add(other.MaxHp);
            }
        }

        long availableCount = checked(rangeSize - usedHp.Count);
        int rolledHp;
        if (availableCount > 0)
        {
            rolledHp = SelectAvailableHp(min, max, usedHp, NextHpOffset(rng, availableCount));
        }
        else if (max < int.MaxValue)
        {
            // 原版可选集合为空时直接 rng.NextInt(min, max + 1)。取值与 min + NextInt(0, rangeSize) 相同，
            // 但随机数账本按调用参数逐项比对，要保留原版的调用形式。
            rolledHp = rng.NextInt(min, max + 1);
        }
        else
        {
            // max 为 int.MaxValue 时 max + 1 溢出，原版在这里不可达；保留溢出安全的偏移抽取。
            rolledHp = checked((int)checked((long)min + NextHpOffset(rng, rangeSize)));
        }
        MaxHp = rolledHp;
        CurrentHp = rolledHp;
    }

    private static long NextHpOffset(Rng rng, long upperExclusive)
    {
        if (upperExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(upperExclusive));
        }

        if (upperExclusive <= int.MaxValue)
        {
            return rng.NextInt(0, checked((int)upperExclusive));
        }

        return checked((long)rng.NextUnsignedLong(checked((ulong)upperExclusive)));
    }

    private static int SelectAvailableHp(
        int min,
        int max,
        IEnumerable<int> usedHp,
        long selectedOrdinal)
    {
        long candidate = min;
        foreach (int used in usedHp)
        {
            long gap = checked((long)used - candidate);
            if (selectedOrdinal < gap)
            {
                return checked((int)checked(candidate + selectedOrdinal));
            }

            selectedOrdinal = checked(selectedOrdinal - gap);
            candidate = checked((long)used + 1L);
        }

        long selected = checked(candidate + selectedOrdinal);
        if (selected < min || selected > max)
        {
            throw new InvalidOperationException("Available monster HP selection exceeded its inclusive range.");
        }

        return checked((int)selected);
    }

    /// <summary>格挡吸收伤害。Unblockable 伤害完全穿透。逐字移植。</summary>
    public decimal DamageBlockInternal(decimal amount, ValueProp props)
    {
        decimal blocked = props.HasFlag(ValueProp.Unblockable) ? 0m : Math.Min(Block, amount);
        // 故意保留原游戏的截断行为：Block 按 (int)blocked 扣减,但方法返回未截断的 blocked——
        // amount 若为小数（Task 9 引入百分比类 power 修正后可能出现）,返回值与 Block 实际扣减量可相差 <1。
        // 这是 v0.109 反编译源码的逐字行为,不是移植 bug；修改前务必先核对真实游戏源码。
        Block -= (int)blocked;
        return blocked;
    }

    /// <summary>扣除未被格挡吸收的伤害。逐字移植（含击杀/超杀判定）。</summary>
    public DamageResult LoseHpInternal(decimal amount, ValueProp props)
    {
        bool willKill = CurrentHp > 0 && amount >= (decimal)CurrentHp;
        int hpBefore = CurrentHp;
        int loss = (int)Math.Clamp(amount, 0m, 999999999m);
        CurrentHp = Math.Max(CurrentHp - loss, 0);
        CumulativeHpLost = checked(CumulativeHpLost + hpBefore - CurrentHp);
        return new DamageResult(this, props)
        {
            UnblockedDamage = hpBefore - CurrentHp,
            WasTargetKilled = willKill,
            OverkillDamage = willKill ? Math.Max(loss - hpBefore, 0) : 0,
        };
    }

    public void GainBlockInternal(decimal amount)
    {
        Block = (int)Math.Clamp(Block + amount, 0m, 999999999m);
    }

    public void LoseBlockInternal(decimal amount)
    {
        Block = (int)Math.Clamp(Block - amount, 0m, 999999999m);
    }

    public void HealInternal(decimal amount)
    {
        CurrentHp = (int)Math.Clamp(CurrentHp + amount, 0m, MaxHp);
    }

    public void SetCurrentHpInternal(decimal amount)
    {
        CurrentHp = (int)Math.Clamp(amount, 0m, MaxHp);
    }

    public void SetMaxHpInternal(decimal amount)
    {
        MaxHp = (int)Math.Clamp(amount, 1m, 999999999m);
        CurrentHp = Math.Min(CurrentHp, MaxHp);
    }

    public void ApplyPowerInternal(PowerModel power)
    {
        _powers.Add(power);
    }

    public void RemovePowerInternal(PowerModel power)
    {
        _powers.Remove(power);
    }

    internal void ResetCumulativeHpLost() => CumulativeHpLost = 0;

    internal void RestoreCumulativeHpLostFrom(Creature source) => CumulativeHpLost = source.CumulativeHpLost;

    public void InvokeDiedEvent() => Died?.Invoke(this);

    public void AssignSlotName(string? slotName)
    {
        SlotName = slotName;
    }

    public void BeforeTurnStart(CombatSide side)
    {
        foreach (PowerModel power in _powers)
        {
            power.AmountOnTurnStart = power.Amount;
        }
    }

    public T? GetPower<T>() where T : PowerModel => _powers.OfType<T>().FirstOrDefault();

    public bool HasPower<T>() where T : PowerModel => GetPower<T>() != null;
}
