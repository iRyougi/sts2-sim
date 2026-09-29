namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Combat.StateDescription;

/// <summary>Waterfall Giant's pressure buildup, knockout, stunned turn, and final steam explosion.</summary>
public sealed class WaterfallGiant : MonsterModel
{
    private int _currentPressureGunDamage;
    private int _steamEruptionDamage;
    private MoveState _aboutToBlowState = null!;

    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 250, 240);
    public override int MaxInitialHp => MinInitialHp;
    public int SiphonHeal => Ascension(AscensionLevel.ToughEnemies, 15, 10);

    private int PressurizeAmount => Ascension(AscensionLevel.DeadlyEnemies, 20, 15);
    private int StompDamage => Ascension(AscensionLevel.DeadlyEnemies, 16, 15);
    private int RamDamage => Ascension(AscensionLevel.DeadlyEnemies, 11, 10);
    private int PressureUpDamage => Ascension(AscensionLevel.DeadlyEnemies, 14, 13);
    private int BasePressureGunDamage => Ascension(AscensionLevel.DeadlyEnemies, 23, 20);
    private const int PressureGunIncrease = 5;

    public override bool ShouldDisappearFromDoom() => !Creature.HasPower<SteamEruptionPower>();

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        _currentPressureGunDamage = BasePressureGunDamage;
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var pressurize = new MoveState("PRESSURIZE_MOVE", PressurizeMove, new BuffIntent());
        // Cloned move graphs are rebuilt before their creatures are attached to combat;
        // defer ascension lookup so their intents retain the same damage as the live graph.
        var stomp = new MoveState(
            "STOMP_MOVE", StompMove, new SingleAttackIntent(() => StompDamage), new DebuffIntent(), new BuffIntent());
        var ram = new MoveState("RAM_MOVE", RamMove, new SingleAttackIntent(() => RamDamage), new BuffIntent());
        var siphon = new MoveState("SIPHON_MOVE", SiphonMove, new HealIntent(), new BuffIntent());
        var pressureGun = new MoveState(
            "PRESSURE_GUN_MOVE", PressureGunMove, new SingleAttackIntent(() => _currentPressureGunDamage),
            new BuffIntent());
        var pressureUp = new MoveState(
            "PRESSURE_UP_MOVE", PressureUpMove, new SingleAttackIntent(() => PressureUpDamage), new BuffIntent());
        _aboutToBlowState = new MoveState("ABOUT_TO_BLOW_MOVE", AboutToBlowMove, new StunIntent())
        {
            MustPerformOnceBeforeTransitioning = true,
        };
        var explode = new MoveState("EXPLODE_MOVE", ExplodeMove, new DeathBlowIntent(() => _steamEruptionDamage));

        pressurize.FollowUpState = stomp;
        stomp.FollowUpState = ram;
        ram.FollowUpState = siphon;
        siphon.FollowUpState = pressureGun;
        pressureGun.FollowUpState = pressureUp;
        pressureUp.FollowUpState = stomp;
        _aboutToBlowState.FollowUpState = explode;
        explode.FollowUpState = explode;

        return new MonsterMoveStateMachine(
            [pressurize, stomp, ram, siphon, pressureGun, pressureUp, explode, _aboutToBlowState], pressurize);
    }

    private async Task PressurizeMove(IReadOnlyList<Creature> targets)
    {
        await PowerCmd.Apply<SteamEruptionPower>(Creature.CombatState!, Creature, PressurizeAmount, Creature, null);
    }

    private async Task PressureUpMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PressureUpDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<SteamEruptionPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }

    private async Task StompMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(StompDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 1m, Creature, null);
        }
        await PowerCmd.Apply<SteamEruptionPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }

    private async Task RamMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(RamDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<SteamEruptionPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }

    private async Task SiphonMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.Heal(Creature, SiphonHeal * Creature.CombatState!.Players.Count);
        await PowerCmd.Apply<SteamEruptionPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }

    private async Task PressureGunMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(_currentPressureGunDamage).FromMonster(this).Execute();
        _currentPressureGunDamage += PressureGunIncrease;
        await PowerCmd.Apply<SteamEruptionPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }

    private async Task AboutToBlowMove(IReadOnlyList<Creature> targets)
    {
        SteamEruptionPower? steamEruption = Creature.GetPower<SteamEruptionPower>();
        _steamEruptionDamage = steamEruption?.Amount ?? 0;
        if (steamEruption is not null)
        {
            await PowerCmd.Remove(steamEruption);
        }
    }

    private async Task ExplodeMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(_steamEruptionDamage).FromMonster(this).Execute();
        await CreatureCmd.Kill(Creature);
    }

    public async Task TriggerAboutToBlowState()
    {
        await CreatureCmd.SetMaxAndCurrentHp(Creature, 999999999m);
        // 偏离 #324：省略 HpDisplay.InfiniteWithoutNumbers 的纯 UI 显示；
        // 真实的 999999999 最大/当前 HP、眩晕回合与爆炸伤害完整保留。
        SetMoveImmediate(_aboutToBlowState, forceTransition: true);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _aboutToBlowState = null!;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_currentPressureGunDamage);
        builder.Append(_steamEruptionDamage);
    }
}
