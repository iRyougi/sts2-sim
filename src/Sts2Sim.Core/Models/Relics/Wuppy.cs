using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.RestSite;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using WuppyMonster = Sts2Sim.Core.Models.Monsters.Wuppy;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.113.1 <c>MegaCrit.Sts2.Core.Models.Relics.Wuppy</c>：事件宠物遗物。保存 Wuppy 的
/// <see cref="CurrentHp"/>/<see cref="MaxHp"/>（60 强 / 30 弱），每场战斗开始召唤带
/// <see cref="WuppysLoyaltyPower"/> 的真实宠物并按保存值设 HP，宠物 HP 变化回写遗物；营火提供 RESTORE。
/// 偏离 #160：强/弱标题、计数显示与 Disabled 状态图标省略（只影响呈现）。</summary>
public sealed class Wuppy : RelicModel
{
    public const int WeakHp = 30;
    public const int StrongHp = 60;

    private bool _currentHpSet;
    private int _currentHp;
    private int _maxHp;
    // 原版 CombatWuppy 字段。战斗克隆时置空，由当前宠物列表解析（见 CombatWuppy）。
    private Creature? _combatWuppy;

    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool AddsPet => true;

    public int CurrentHp
    {
        get => _currentHp;
        set
        {
            AssertMutable();
            _currentHp = value;
            _currentHpSet = true;
        }
    }

    public int MaxHp
    {
        get => _maxHp;
        set
        {
            AssertMutable();
            _maxHp = value;
        }
    }

    public Creature? CombatWuppy =>
        _combatWuppy ?? Owner?.PlayerCombatState?.Pets.FirstOrDefault(pet => pet.Monster is WuppyMonster);

    public override async Task AfterObtained()
    {
        if (MaxHp == 0) MaxHp = StrongHp;
        if (!_currentHpSet) CurrentHp = MaxHp;
        if (Owner.RunState.CurrentRoom is CombatRoom) await SummonPet();
    }

    public override Task BeforeCombatStart() => SummonPet();

    private async Task SummonPet()
    {
        var combatState = Owner.Creature.CombatState
            ?? throw new InvalidOperationException("Wuppy can only be summoned in combat.");
        var monster = (WuppyMonster)ModelDb.Monster<WuppyMonster>().MutableClone();
        AssertMutable();
        _combatWuppy = combatState.CreateCreature(monster, Owner.Creature.Side, slotName: null);
        await PowerCmd.Apply<WuppysLoyaltyPower>(combatState, _combatWuppy, 1m, null, null);
        // v0.114.0 (#1110): native sets the pet's HP through the internal setters, so no HP hook runs on summon
        // (v0.113.1 went through CreatureCmd.SetMaxHp / SetCurrentHp).
        _combatWuppy.SetMaxHpInternal(MaxHp);
        _combatWuppy.SetCurrentHpInternal(CurrentHp);
        await PlayerCmd.AddPet(_combatWuppy, Owner);
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        Creature? combatWuppy = CombatWuppy;
        if (combatWuppy is null || creature != combatWuppy) return Task.CompletedTask;
        CurrentHp = combatWuppy.CurrentHp;
        return Task.CompletedTask;
    }

    public override void ModifyAvailableRestSiteDecisions(IRunState runState, Player player, List<RestSiteDecision> decisions)
    {
        if (!ReferenceEquals(player, Owner)) return;
        if (decisions.Any(decision => decision is RestoreRestSiteOption)) return;
        decisions.Add(new RestoreRestSiteOption(CurrentHp < MaxHp));
    }

    internal override void RestoreCombatCloneReferencesFrom(
        RelicModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        _combatWuppy = null;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_currentHpSet);
        builder.Append(CurrentHp);
        builder.Append(MaxHp);
    }
}
