using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat;

/// <summary>
/// 战斗回合循环引擎。偏离 #29：把游戏 <c>CombatManager</c> 单例（含 Godot 节点/多人房间/action queue 同步）
/// 精简为无状态的实例方法集合;调用序与 hook 调用点逐字对照原文保留。
/// </summary>
public sealed class CombatEngine
{
    private const decimal BaseHandDrawCount = 5m;
    private readonly ICombatObserver? _observer;
    private bool _endPlayerTurnRequested;
    private int _cardActionBoundaryDepth;

    public CombatEngine(CombatState state)
        : this(state, observer: null)
    {
    }

    internal CombatEngine(CombatState state, ICombatObserver? observer)
    {
        State = state;
        _observer = observer;
        State.AttachEngine(this);
    }

    internal CombatEngine CloneFor(CombatState state)
    {
        var clone = new CombatEngine(state, observer: null)
        {
            IsInProgress = IsInProgress,
            IsStarting = IsStarting,
            Won = Won,
        };
        return clone;
    }

    internal static decimal GetModifiedHandDraw(ICombatState combatState, Player player)
    {
        return Hook.ModifyHandDraw(combatState, player, BaseHandDrawCount);
    }

    public CombatState State { get; }
    internal ICombatObserver? Observer => _observer;


    public bool IsInProgress { get; private set; }

    /// <summary>Native setup window: combat hooks run before IsInProgress becomes true.</summary>
    public bool IsStarting { get; private set; }

    /// <summary>Native IsEnding: victory/pending loss while combat is still in progress.
    /// Single-player pending loss uses the same all-player-dead condition as CheckWinCondition.</summary>
    public bool IsEnding => IsInProgress && (HasLost || HasWon);

    public bool IsOverOrEnding => IsEnding || !IsInProgress;

    private bool HasLost => !State.PlayerCreatures.Any(creature => creature.IsAlive);

    private bool HasWon => !State.Enemies.Any(enemy => enemy.IsAlive && enemy.IsPrimaryEnemy) &&
        !Hook.ShouldStopCombatFromEnding(State);

    public bool Won { get; private set; }

    /// <summary>战斗开局：把每个玩家的牌库洗进抽牌堆、初始化怪物 AI,然后进入玩家首回合。</summary>
    public Task StartCombatAsync() => StartCombatAsync(afterSetup: null);

    // Keep one combat_start RNG scope across setup, room-entry hooks and combat startup.
    internal async Task StartCombatAsync(Func<Task>? afterSetup)
    {
        IsStarting = true;
        using IDisposable rngScope = State.BeginPhaseRngScope("combat_start");
        foreach (Player player in State.Players)
        {
            player.ResetCombatState();
            player.PopulateCombatState(State.NextShuffleRng());
            // Rooms attach players before spawning enemies. Preserve direct engine callers,
            // which may construct a CombatState without first entering a room.
            if (!State.ContainsCreature(player.Creature))
                State.AddPlayerCreature(player.Creature);
        }

        foreach (Creature enemy in State.Enemies)
        {
            enemy.Monster!.SetUpForCombat();
        }

        if (afterSetup is not null)
        {
            await afterSetup();
        }

        foreach (Creature enemy in State.Enemies)
        {
            await enemy.Monster!.AfterAddedToRoom();
        }

        IsInProgress = true;
        IsStarting = false;
        await Hook.BeforeCombatStart(State);
        _observer?.CombatStarted(State);
        await StartTurnAsync();
    }

    /// <summary>
    /// 某一方开始回合。逐字对照 <c>CombatManager.StartTurn</c> 保留调用序与 hook 调用点——包括怪物下一招的
    /// 滚动时机：真实游戏在玩家回合开始、`Hook.BeforeSideTurnStart` 触发之后、格挡清空之前重滚敌方意图
    /// （见 <c>Creature.PrepareForNextTurn</c> 的调用点),这样玩家在自己的回合里看到的意图才和敌方回合真正
    /// 执行的招式一致——本计划早期草稿把这一步错放在"进入敌方回合时",会导致展示的意图和执行的招式不同步,
    /// 已改正。
    /// </summary>
    public Task StartTurnAsync() => StartTurnAsync(extraTurnPlayer: null);

    private async Task StartTurnAsync(Player? extraTurnPlayer)
    {
        using IDisposable rngScope = State.BeginPhaseRngScope(
            extraTurnPlayer is null ? "turn_start" : "extra_turn_start");
        State.IsPlayerExtraTurn = extraTurnPlayer is not null;
        IReadOnlyList<Creature> participants = extraTurnPlayer is null
            ? State.GetCreaturesOnSide(State.CurrentSide)
            : new[] { extraTurnPlayer.Creature };
        foreach (Creature creature in participants)
        {
            creature.BeforeTurnStart(State.CurrentSide);
        }
        await Hook.BeforeSideTurnStart(State, State.CurrentSide, participants);

        if (State.CurrentSide == CombatSide.Player && extraTurnPlayer is null)
        {
            foreach (Creature enemy in State.Enemies)
            {
                if (enemy.IsAlive || !Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(State, enemy))
                {
                    enemy.Monster!.RollMove(State.PlayerCreatures);
                }
            }
        }

        foreach (Creature creature in participants)
        {
            // The real Creature.AfterTurnStart skips block clearing on a player's first turn. This engine
            // increments TurnNumber later in SetupPlayerTurnAsync, so zero is the equivalent pre-increment state.
            bool isFirstPlayerTurn = State.CurrentSide == CombatSide.Player &&
                                     creature.Player?.PlayerCombatState?.TurnNumber == 0;
            if (!isFirstPlayerTurn)
            {
                if (Hook.ShouldClearBlock(State, creature, out AbstractModel? preventer))
                {
                    creature.LoseBlockInternal(creature.Block);
                }
                else
                {
                    await Hook.AfterPreventingBlockClear(State, preventer!, creature);
                }
            }
        }

        // Source CombatManager.StartTurn makes a second pass over all participants
        // after the turn-start phase, even when block clearing was skipped or prevented.
        foreach (Creature creature in participants)
        {
            await Hook.AfterBlockCleared(State, creature);
        }

        if (State.CurrentSide == CombatSide.Player)
        {
            IReadOnlyList<Player> players = extraTurnPlayer is null
                ? State.Players
                : new[] { extraTurnPlayer };
            foreach (Player player in players)
            {
                await SetupPlayerTurnAsync(player);
            }
        }

        if (State.CurrentSide == CombatSide.Player)
        {
            _observer?.PlayerTurnPrepared(State);
            await ExecuteCardActionBoundaryAsync(async () =>
            {
                IReadOnlyList<Player> players = extraTurnPlayer is null
                    ? State.Players
                    : new[] { extraTurnPlayer };
                // Start-turn effects can play cards or consume automatic potions. Prepare recording
                // first, and keep all player phases inside the same outer action boundary.
                foreach (Player player in players)
                {
                    await Hook.AfterPlayerTurnStart(State, player);
                }
                await Hook.AfterSideTurnStart(State, State.CurrentSide, participants);
                foreach (Player player in players)
                {
                    if (player.PlayerCombatState is not null)
                        await player.PlayerCombatState.OrbQueue.AfterTurnStart(State);
                }
                foreach (Player player in players.Where(player => player.Creature.IsAlive))
                {
                    player.PlayerCombatState!.Phase = PlayerTurnPhase.AutoPrePlay;
                    await CardPileCmd.CheckForEmptyHand(State, player);
                    await Hook.AfterAutoPrePlayPhaseEntered(State, player);
                    player.PlayerCombatState.Phase = PlayerTurnPhase.Play;
                }

                // Native StartTurn settles the combat after the auto pre-play phase, so a turn-start
                // kill (e.g. MercuryHourglass on the last enemy) never opens a play phase in IsEnding.
                CheckWinCondition();
                _observer?.PlayerTurnStarted(State);
                return true;
            });
        }

        if (State.CurrentSide == CombatSide.Enemy)
        {
            await Hook.AfterSideTurnStart(State, State.CurrentSide, participants);
            await ExecuteEnemyTurnAsync();
        }
    }

    /// <summary>供调用方（demo/后续 RL 环境）驱动：打出一张手牌。战斗已结束或卡牌不可玩时是安全的无操作。</summary>
    public Task PlayCardAsync(Player player, CardModel card, Creature? target) =>
        PlayCardWithResultAsync(player, card, target);

    /// <summary>Plays a card and returns the first resolved play for reporting consumers.</summary>
    public async Task<CardPlay?> PlayCardWithResultAsync(Player player, CardModel card, Creature? target)
    {
        if (!IsInProgress)
        {
            return null;
        }

        if (!card.CanPlay(out _))
        {
            return null;
        }

        return await ExecuteCardActionBoundaryAsync(
            () => card.PlayWithResultAsync(target));
    }

    /// <summary>结束玩家回合：弃置未打出的手牌 → 切到敌方 → 进入敌方回合。</summary>
    public async Task EndPlayerTurnAsync()
    {
        if (!IsInProgress)
        {
            return;
        }

        using IDisposable rngScope = State.BeginPhaseRngScope("player_turn_end");

        _endPlayerTurnRequested = false;
        // Native EndPlayerTurnPhaseOne enters AutoPostPlay and finishes its listeners
        // before changing the phase to End or invoking BeforeSideTurnEnd.
        foreach (Player player in State.Players)
        {
            player.PlayerCombatState!.Phase = PlayerTurnPhase.AutoPostPlay;
            await Hook.AfterAutoPostPlayPhaseEntered(State, player);
            player.PlayerCombatState.Phase = PlayerTurnPhase.End;
        }

        if (CheckWinCondition())
        {
            _observer?.PlayerTurnEnded(State);
            return;
        }

        await Hook.BeforeSideTurnEndVeryEarly(State, CombatSide.Player, State.Allies);
        await Hook.BeforeSideTurnEndEarly(State, CombatSide.Player, State.Allies);

        await Hook.BeforeSideTurnEnd(State, CombatSide.Player, State.Allies);
        if (CheckWinCondition())
        {
            _observer?.PlayerTurnEnded(State);
            return;
        }

        foreach (Player player in State.Players)
        {
            await DoTurnEndAsync(State, player);
        }

        if (CheckWinCondition())
        {
            _observer?.PlayerTurnEnded(State);
            return;
        }

        // CombatManager 阶段一的末尾：逐个玩家 BeforeFlush，之后再检查一次胜负。
        foreach (Player player in State.Players)
        {
            await Hook.BeforeFlush(State, player);
        }

        if (CheckWinCondition())
        {
            _observer?.PlayerTurnEnded(State);
            return;
        }

        // CombatManager 阶段二：弃掉剩下的手牌。虚无牌与回合末效果牌已在 DoTurnEndAsync 里离开手牌。
        foreach (Player player in State.Players)
        {
            // #102 closed: every combat listener can veto the owner's hand flush.
            bool retainWholeHand = !Hook.ShouldFlush(State, player);
            var flushed = new List<CardModel>();
            var retained = new List<CardModel>();
            foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToList())
            {
                if (!card.HasKeyword(CardKeyword.Retain) && !retainWholeHand)
                {
                    flushed.Add(card);
                    CardPileCmd.Add(card, PileType.Discard);
                }
                else
                {
                    retained.Add(card);
                }
            }

            await Hook.AfterFlush(State, player, flushed, retained);
            player.PlayerCombatState.EndOfTurnCleanup();
        }

        await Hook.AfterSideTurnEnd(State, CombatSide.Player, State.Allies);

        Player? extraTurnPlayer = State.Players.FirstOrDefault(player =>
            player.Creature.IsAlive && Hook.ShouldTakeExtraTurn(State, player));
        if (extraTurnPlayer is not null)
        {
            // Deviation #157: the simulator is currently single-player, so the source snapshot of
            // extra-turn players is represented by this one selected player. Consumption still occurs
            // before the extra turn starts, matching CombatManager's authoritative sequencing.
            await Hook.AfterTakingExtraTurn(State, extraTurnPlayer);
            _observer?.PlayerTurnEnded(State);
            State.CurrentSide = CombatSide.Player;
            NotifySideSwitch();
            await StartTurnAsync(extraTurnPlayer);
            return;
        }

        State.IsPlayerExtraTurn = false;
        State.CurrentSide = CombatSide.Enemy;
        NotifySideSwitch();
        _observer?.EnemyTurnStarting(State);
        await StartTurnAsync();
    }

    /// <summary>原版 <c>CombatManager.DoTurnEnd</c> / <c>DoTurnEndCards</c>：按手牌顺序把牌分两类——有回合末效果的，
    /// 以及其余的虚无牌；先把虚无牌逐张消耗，再把回合末效果牌逐张移入打出区、结算效果，然后放到弃牌堆底
    /// （本身是虚无的则消耗）。之后才在阶段二弃掉剩下的手牌，所以弃牌堆里回合末效果牌排在当回合其他手牌之前。</summary>
    internal static async Task DoTurnEndAsync(ICombatState combatState, Player player)
    {
        PlayerCombatState state = player.PlayerCombatState!;
        await state.OrbQueue.BeforeTurnEnd(combatState);
        if (combatState.IsOverOrEnding())
            return;
        var turnEndCards = new List<CardModel>();
        var etherealCards = new List<CardModel>();
        foreach (CardModel card in state.Hand.Cards)
        {
            if (card.HasTurnEndInHandEffectInternal)
            {
                turnEndCards.Add(card);
            }
            else if (card.HasKeyword(CardKeyword.Ethereal))
            {
                // 原版还要求 Hook.ShouldEtherealTrigger，正式版里没有任何模型重写它，恒为真。
                etherealCards.Add(card);
            }
        }

        foreach (CardModel card in etherealCards)
        {
            await CardPileCmd.Exhaust(combatState, card, causedByEthereal: true);
        }

        foreach (CardModel card in turnEndCards)
        {
            CardPileCmd.Add(card, PileType.Play);
            await card.ResolveTurnEndInHandEffect();
            card.InvokeExecutionFinished();
            if (card.HasKeyword(CardKeyword.Ethereal))
            {
                await CardPileCmd.Exhaust(combatState, card, causedByEthereal: true);
            }
            else
            {
                CardPileCmd.Add(card, PileType.Discard, CardPilePosition.Bottom);
            }
        }
    }

    /// <summary>
    /// 胜负判定,逐字移植 <c>CombatManager.IsEnding</c> 的单人简化版（无多人加时/待定败北的竞态规避)：
    /// 玩家全灭优先判负；否则若还有存活的主要敌人则未结束；若 <c>Hook.ShouldStopCombatFromEnding</c> 拦截则未结束；
    /// 否则判胜。
    /// </summary>
    public bool CheckWinCondition()
    {
        if (!IsInProgress)
        {
            return true;
        }

        if (HasLost)
        {
            IsInProgress = false;
            Won = false;
            return true;
        }

        if (!HasWon)
        {
            return false;
        }

        IsInProgress = false;
        Won = true;
        return true;
    }

    /// <summary>逐字移植 <c>CombatManager.SetupPlayerTurn</c>：重置能量、增加回合数并抽牌。</summary>
    private async Task SetupPlayerTurnAsync(Player player)
    {
        player.PlayerCombatState!.Phase = PlayerTurnPhase.Start;
        if (Hook.ShouldPlayerResetEnergy(State, player))
        {
            player.PlayerCombatState!.ResetEnergy();
        }
        else
        {
            player.PlayerCombatState!.AddMaxEnergyToCurrent();
        }

        await Hook.AfterEnergyReset(State, player);

        player.PlayerCombatState!.TurnNumber++;
        await Hook.BeforeHandDraw(State, player);
        decimal handDrawCount = Hook.ModifyHandDraw(State, player, BaseHandDrawCount,
            out IEnumerable<AbstractModel> modifiers);
        await Hook.AfterModifyingHandDraw(State, modifiers);
        await CardPileCmd.Draw(State, (int)handDrawCount, player, fromHandDraw: true);
    }

    internal async Task<T> ExecuteCardActionBoundaryAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        _cardActionBoundaryDepth++;
        bool completed = false;
        try
        {
            T result = await action();
            completed = true;
            return result;
        }
        finally
        {
            _cardActionBoundaryDepth--;
            if (_cardActionBoundaryDepth == 0)
            {
                if (completed)
                {
                    await ResolveRequestedEndPlayerTurnAsync();
                }
                else
                {
                    CancelRequestedEndPlayerTurn();
                }
            }
        }
    }

    internal void RequestEndPlayerTurn()
    {
        if (IsInProgress && State.CurrentSide == CombatSide.Player)
        {
            _endPlayerTurnRequested = true;
        }
    }

    /// <summary>Reports whether a player has requested ending their current player turn.</summary>
    public bool IsPlayerReadyToEndTurn(Player player) =>
        _endPlayerTurnRequested || !IsInProgress || State.CurrentSide != CombatSide.Player ||
        player.PlayerCombatState?.Phase == PlayerTurnPhase.End;

    internal void CancelRequestedEndPlayerTurn() => _endPlayerTurnRequested = false;

    internal async Task ResolveRequestedEndPlayerTurnAsync()
    {
        if (!_endPlayerTurnRequested)
        {
            return;
        }

        _endPlayerTurnRequested = false;
        await EndPlayerTurnAsync();
    }

    /// <summary>逐字移植 <c>CombatManager.ExecuteEnemyTurn</c>：怪物按登记顺序依次行动,随时检查胜负。</summary>
    private async Task ExecuteEnemyTurnAsync()
    {
        foreach (Creature enemy in State.Enemies.ToList())
        {
            if (!enemy.IsAlive && Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(State, enemy))
            {
                continue;
            }

            MonsterModel monster = enemy.Monster!;
            // Creature.TakeTurn：本回合刚加入的怪物（换边前召唤的）不行动，它的第一招要到下个玩家回合开始才掷。
            if (monster.SpawnedThisTurn)
            {
                continue;
            }

            if (_observer is null || monster.NextMove is not { } observedMove)
            {
                await monster.PerformMove();
            }
            else
            {
                string moveId = observedMove.StateId;
                try
                {
                    _observer.EnemyMoveStarted(enemy, moveId);
                    await monster.PerformMove();
                    _observer.EnemyMoveFinished(enemy, moveId);
                }
                catch
                {
                    TryAbortEnemyMove(_observer, enemy, moveId);
                    throw;
                }
            }
            if (CheckWinCondition())
            {
                _observer?.PlayerTurnEnded(State);
                return;
            }
        }

        await EndEnemyTurnAsync();
    }

    private static void TryAbortEnemyMove(
        ICombatObserver observer,
        Creature source,
        string moveId)
    {
        try
        {
            observer.EnemyMoveAborted(source, moveId);
        }
        catch
        {
            // Preserve the original core or observer callback exception.
        }
    }

    private async Task EndEnemyTurnAsync()
    {
        using IDisposable rngScope = State.BeginPhaseRngScope("enemy_turn_end");
        await Hook.BeforeSideTurnEndVeryEarly(State, CombatSide.Enemy, State.Enemies);
        await Hook.BeforeSideTurnEndEarly(State, CombatSide.Enemy, State.Enemies);
        await Hook.BeforeSideTurnEnd(State, CombatSide.Enemy, State.Enemies);
        foreach (Player player in State.Players)
        {
            player.PlayerCombatState!.EndOfTurnCleanup();
        }

        await Hook.AfterSideTurnEnd(State, CombatSide.Enemy, State.Enemies);

        if (CheckWinCondition())
        {
            _observer?.PlayerTurnEnded(State);
            return;
        }

        _observer?.PlayerTurnEnded(State);
        State.CurrentSide = CombatSide.Player;
        State.RoundNumber++;
        NotifySideSwitch();
        await StartTurnAsync();
    }

    /// <summary><c>CombatManager.SwitchSides</c> 末尾对全体生物调用 <c>OnSideSwitch</c>：清掉怪物的
    /// <c>SpawnedThisTurn</c>（玩家侧的实现为空）。开局怪物也带着这个标记，第一次换边后才会行动。</summary>
    private void NotifySideSwitch()
    {
        foreach (Creature creature in State.Allies.Concat(State.Enemies).ToList())
        {
            creature.Monster?.OnSideSwitch();
        }
    }
}
