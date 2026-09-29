using Sts2Sim.Core.Content;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Demo;

/// <summary>
/// console 战斗回放 demo 的核心逻辑（脱离 Console,方便单元测试）：储君 vs TrainingDummy,
/// 固定策略"手牌里能打的牌全部打出",支持指定 seed。这是 Plan 03 要求的可见成果任务。
/// </summary>
public static class CombatReplayDemo
{
    public const int MaxRounds = 50;

    public sealed record Result(bool Won, int Rounds, int PlayerFinalHp, int MonsterFinalHp);

    private sealed class DemoRunState : IRunState
    {
        private readonly List<Player> _players = new();

        public DemoRunState(string seed) => Rng = new RunRngSet(seed);

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public RunRngSet Rng { get; }

        public IReadOnlyList<Player> Players => _players;

        public int TotalFloor => 0;

        public Rooms.AbstractRoom? CurrentRoom => null;

        public void AddPlayer(Player player) => _players.Add(player);

        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            childCombatState?.IterateHookListeners() ?? Array.Empty<AbstractModel>();
    }

    /// <summary>依赖 <c>ModelDb.Init</c> 的幂等跳过语义,可安全重复调用。</summary>
    public static void EnsureModelsRegistered() => ModelDb.Init(ContentRegistry.AllTypes);

    public static async Task<Result> RunAsync(string seed, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);

        EnsureModelsRegistered();

        var runState = new DemoRunState(seed);
        var combatState = new CombatState(runState);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        var dummyModel = (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone();
        Creature dummy = combatState.AddMonster(dummyModel, CombatSide.Enemy);
        var engine = new CombatEngine(combatState);

        log($"=== Sts2Sim 战斗回放 demo (seed=\"{seed}\") ===");
        await engine.StartCombatAsync();

        while (engine.IsInProgress && combatState.RoundNumber <= MaxRounds)
        {
            log(string.Empty);
            log($"--- 第 {combatState.RoundNumber} 回合 ---");
            log($"怪物意图：{DescribeIntent(dummyModel, combatState.Allies)}");
            log($"玩家：HP {player.Creature.CurrentHp}/{player.Creature.MaxHp}  格挡 {player.Creature.Block}  能量 {player.PlayerCombatState!.Energy}");
            log($"怪物：HP {dummy.CurrentHp}/{dummy.MaxHp}  格挡 {dummy.Block}");

            bool playedCard;
            do
            {
                playedCard = false;
                foreach (CardModel card in player.PlayerCombatState.Hand.Cards.ToList())
                {
                    if (!card.CanPlay(out _))
                    {
                        continue;
                    }

                    Creature? target = card.TargetType == TargetType.AnyEnemy ? dummy : null;
                    int monsterHpBefore = dummy.CurrentHp;
                    int playerBlockBefore = player.Creature.Block;
                    int energyBefore = player.PlayerCombatState.Energy;
                    int starsBefore = player.PlayerCombatState.Stars;
                    int weakBefore = GetPowerAmount<WeakPower>(dummy);
                    int vulnerableBefore = GetPowerAmount<VulnerablePower>(dummy);

                    await engine.PlayCardAsync(player, card, target);
                    LogCardPlay(log, card, player, dummy, monsterHpBefore, playerBlockBefore, energyBefore, starsBefore, weakBefore, vulnerableBefore);
                    engine.CheckWinCondition();
                    playedCard = true;
                    break;
                }
            }
            while (engine.IsInProgress && playedCard);
            if (engine.IsInProgress)
            {
                await engine.EndPlayerTurnAsync();
                log($"敌方回合结算：玩家 HP -> {player.Creature.CurrentHp}/{player.Creature.MaxHp}");
            }
        }

        log(string.Empty);
        log(engine.Won ? "=== 胜利！===" : "=== 失败……===");
        log($"共进行 {combatState.RoundNumber} 回合。玩家剩余 HP：{player.Creature.CurrentHp}/{player.Creature.MaxHp}");

        return new Result(engine.Won, combatState.RoundNumber, player.Creature.CurrentHp, dummy.CurrentHp);
    }


    private static void LogCardPlay(
        Action<string> log,
        CardModel card,
        Player player,
        Creature monster,
        int monsterHpBefore,
        int playerBlockBefore,
        int energyBefore,
        int starsBefore,
        int weakBefore,
        int vulnerableBefore)
    {
        int damage = monsterHpBefore - monster.CurrentHp;
        log($"出牌：{card.GetType().Name} -> Energy {energyBefore}->{player.PlayerCombatState!.Energy}, Stars {starsBefore}->{player.PlayerCombatState.Stars}, damage {damage}, block {playerBlockBefore}->{player.Creature.Block}, Weak {weakBefore}->{GetPowerAmount<WeakPower>(monster)}, Vulnerable {vulnerableBefore}->{GetPowerAmount<VulnerablePower>(monster)}");
    }

    private static int GetPowerAmount<TPower>(Creature creature)
        where TPower : PowerModel => creature.Powers.OfType<TPower>().Sum(power => power.Amount);

    private static string DescribeIntent(TrainingDummy dummy, IReadOnlyList<Creature> targets)
    {
        AbstractIntent? intent = dummy.NextMove?.Intents.FirstOrDefault();
        return intent switch
        {
            AttackIntent attack => $"{attack.IntentType} {attack.GetTotalDamage(targets, dummy.Creature)}",
            null => "无",
            _ => intent.IntentType.ToString(),
        };
    }
}
