using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Reporting;

internal sealed class CombatRecordingObserver : ICombatObserver
{
    private readonly IRunRecorder _recorder;
    private readonly RunState _runState;
    private readonly CombatRoom _room;
    private readonly Player _player;
    private readonly List<CardModel> _pendingDraws = [];
    private readonly Stack<CardActionContext> _cardActions = [];
    private readonly Stack<EnemyActionContext> _enemyActions = [];
    private readonly Stack<PotionActionContext> _potionActions = [];
    private bool _combatStarted;
    private bool _turnStarted;
    private ActionSnapshot? _finalCombatState;

    public CombatRecordingObserver(
        IRunRecorder recorder,
        RunState runState,
        CombatRoom room)
    {
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _runState = runState ?? throw new ArgumentNullException(nameof(runState));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _player = runState.Players.Single();
    }

    public void CombatStarted(CombatState state)
    {
        if (_combatStarted)
        {
            throw new InvalidOperationException("Combat recording has already started.");
        }

        _recorder.BeginCombat(_runState, _room.RoomType, _room.EncounterName, state);
        _combatStarted = true;
    }

    public void PlayerTurnPrepared(CombatState state)
    {
        EnsureCombatStarted();
        if (_turnStarted)
        {
            throw new InvalidOperationException("A recorded player turn is already active.");
        }

        _recorder.RecordTurnStart(state);
        _turnStarted = true;
        foreach (CardModel card in _pendingDraws)
        {
            _recorder.RecordDraw(card);
        }

        _pendingDraws.Clear();
    }

    public void PlayerTurnStarted(CombatState state) => EnsureTurnStarted();

    public void CardDrawn(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (_turnStarted)
        {
            _recorder.RecordDraw(card);
            return;
        }

        _pendingDraws.Add(card);
    }

    public void CardPlayStarted(CardModel card, Creature? target)
    {
        EnsureTurnStarted();
        ArgumentNullException.ThrowIfNull(card);
        bool hasOwner = _cardActions.Count > 0 ||
                        _potionActions.Count > 0 ||
                        _enemyActions.Count > 0;
        _cardActions.Push(new CardActionContext(
            card,
            target,
            hasOwner ? null : CaptureAction()));
    }

    public void CardPlayFinished(CardModel card, Creature? target, CardPlay? cardPlay)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!_cardActions.TryPop(out CardActionContext? context))
        {
            throw new InvalidOperationException("Combat observer received an unmatched card-play completion.");
        }

        if (!ReferenceEquals(context.Card, card) || !ReferenceEquals(context.Target, target))
        {
            throw new InvalidOperationException("Combat observer received a mismatched card-play completion.");
        }

        if (context.Before is null || cardPlay is null)
        {
            return;
        }

        RecordCardPlay(cardPlay, context.Before);
        FlushPotionActions(context.DeferredPotions);
    }

    public void CardPlayAborted(CardModel card, Creature? target)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (!_cardActions.TryPeek(out CardActionContext? context) ||
            !ReferenceEquals(context.Card, card) ||
            !ReferenceEquals(context.Target, target))
        {
            return;
        }

        _cardActions.Pop();
    }

    public void EnemyMoveStarted(Creature source, string moveId)
    {
        EnsureTurnStarted();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(moveId);
        _enemyActions.Push(new EnemyActionContext(
            source,
            moveId,
            CaptureAction()));
    }

    public void EnemyMoveFinished(Creature source, string moveId)
    {
        if (!_enemyActions.TryPop(out EnemyActionContext? context) ||
            !ReferenceEquals(context.Source, source) ||
            !Equals(context.MoveId, moveId))
        {
            throw new InvalidOperationException("Combat observer received an unmatched enemy-move completion.");
        }

        context.CloseSegment(CaptureAction());
        _recorder.RecordEnemyAction(
            source,
            moveId,
            context.Segments,
            context.ResolvedDamage);
        FlushPotionActions(context.DeferredPotions);
    }

    public void EnemyMoveAborted(Creature source, string moveId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!_enemyActions.TryPeek(out EnemyActionContext? context) ||
            !ReferenceEquals(context.Source, source) ||
            !Equals(context.MoveId, moveId))
        {
            return;
        }

        _enemyActions.Pop();
    }

    public void DamageResolved(Creature? dealer, DamageResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (_potionActions.Count > 0 ||
            !_enemyActions.TryPeek(out EnemyActionContext? context) ||
            !result.Receiver.IsPlayer ||
            result.TotalDamage <= 0)
        {
            return;
        }

        context.ResolvedDamage.Add(result);
    }

    public void PlayerTurnEnded(CombatState state)
    {
        EnsureTurnStarted();
        _recorder.RecordEndTurn(
            SnapshotFactory.SnapshotPlayer(_player),
            SnapshotEnemies());
        _turnStarted = false;
    }

    public void PotionUseStarted(PotionModel potion, Creature? target)
    {
        EnsureTurnStarted();
        ArgumentNullException.ThrowIfNull(potion);
        ActionSnapshot before = CaptureAction();
        EnemyActionContext? enemyOwner = _enemyActions.TryPeek(out EnemyActionContext? enemy)
            ? enemy
            : null;
        enemyOwner?.CloseSegment(before);
        CardActionContext? cardOwner = enemyOwner is null
            ? _cardActions.FirstOrDefault(context => context.Before is not null)
            : null;
        _potionActions.Push(new PotionActionContext(
            potion,
            target,
            before,
            enemyOwner,
            cardOwner));
    }

    public void PotionUseFinished(PotionModel potion, Creature? target)
    {
        if (!_potionActions.TryPop(out PotionActionContext? context) ||
            !ReferenceEquals(context.Potion, potion) ||
            !ReferenceEquals(context.Target, target))
        {
            throw new InvalidOperationException("Combat observer received an unmatched potion-use completion.");
        }

        ActionSnapshot after = CaptureAction();
        var completed = new DeferredPotionAction(
            potion,
            target,
            context.Before,
            after,
            Consumed: !_player.PotionSlots.Contains(potion));
        if (context.EnemyOwner is not null)
        {
            context.EnemyOwner.DeferredPotions.Add(completed);
            context.EnemyOwner.ResumeAfter(after);
        }
        else if (context.CardOwner is not null)
        {
            context.CardOwner.DeferredPotions.Add(completed);
        }
        else
        {
            RecordPotionUse(completed);
        }
    }

    public void PotionUseAborted(PotionModel potion, Creature? target)
    {
        ArgumentNullException.ThrowIfNull(potion);
        if (!_potionActions.TryPeek(out PotionActionContext? context) ||
            !ReferenceEquals(context.Potion, potion) ||
            !ReferenceEquals(context.Target, target))
        {
            return;
        }

        _potionActions.Pop();
        if (context.EnemyOwner is not null)
        {
            context.EnemyOwner.ResumeAfter(CaptureAction());
        }
    }

    private void RecordCardPlay(CardPlay cardPlay, ActionSnapshot before)
    {
        ActionSnapshot after = CaptureAction();
        _recorder.RecordCardPlay(
            cardPlay,
            before.Player,
            after.Player,
            before.Enemies,
            after.Enemies);
    }

    private void FlushPotionActions(IEnumerable<DeferredPotionAction> actions)
    {
        foreach (DeferredPotionAction action in actions)
        {
            RecordPotionUse(action);
        }
    }

    private void RecordPotionUse(DeferredPotionAction action)
    {
        _recorder.RecordPotionUse(
            action.Potion,
            action.Target,
            action.Before.Player,
            action.After.Player,
            action.Before.Enemies,
            action.After.Enemies,
            action.Consumed,
            action.Before.Potions,
            action.After.Potions);
    }

    public void CaptureFinalState()
    {
        EnsureCombatStarted();
        if (_finalCombatState is not null)
        {
            throw new InvalidOperationException("Final combat state has already been captured.");
        }

        _finalCombatState = CaptureAction();
    }

    public void EndCombat(bool victory, IReadOnlyList<RewardsSet> rewardSets)
    {
        EnsureCombatStarted();
        ArgumentNullException.ThrowIfNull(rewardSets);
        ActionSnapshot finalState = _finalCombatState
            ?? throw new InvalidOperationException(
                "Final combat state must be captured before reward selection.");
        _recorder.EndCombat(
            victory,
            finalState.Player,
            finalState.Enemies,
            SnapshotRewards(rewardSets));
    }

    private ActionSnapshot CaptureAction() => new(
        SnapshotFactory.SnapshotPlayer(_player),
        SnapshotEnemies(),
        SnapshotFactory.SnapshotPotions(_player).ToArray());

    private EnemySnapshot[] SnapshotEnemies() =>
        // Dead enemies leave current membership before the action ends; keep their final HP
        // in action snapshots so existing before/after damage correlation retains lethal hits.
        _room.Engine.State.SpawnedEnemies
            .Select(SnapshotFactory.SnapshotEnemy)
            .ToArray();

    private CombatRewards SnapshotRewards(IReadOnlyList<RewardsSet> rewardSets)
    {
        var rewards = new List<Reward>();
        foreach (RewardsSet rewardSet in rewardSets)
        {
            rewards.Add(rewardSet.Gold);
            rewards.Add(rewardSet.Card);
            if (rewardSet.Relic is not null)
            {
                rewards.Add(rewardSet.Relic);
            }

            if (rewardSet.Potion is not null)
            {
                rewards.Add(rewardSet.Potion);
            }

            rewards.AddRange(rewardSet.ExtraRewards);
        }

        string[] cardsTaken = rewards
            .OfType<CardReward>()
            .Select(reward => reward.SelectedOption)
            .OfType<CardModel>()
            .Concat(rewards
                .OfType<SpecialCardReward>()
                .Where(reward => reward.IsResolved)
                .Select(reward => reward.Card))
            .Select(card => card.Id.ToString())
            .ToArray();
        string[] relicsTaken = rewards
            .OfType<RelicReward>()
            .Where(reward => reward.IsResolved)
            .Select(reward => reward.Relic)
            .OfType<RelicModel>()
            .Select(relic => relic.Id.ToString())
            .ToArray();
        string[] potionsTaken = rewards
            .OfType<PotionReward>()
            .Where(reward => reward.IsResolved)
            .Select(reward => reward.Potion)
            .OfType<PotionModel>()
            .Where(potion => _player.PotionSlots.Contains(potion))
            .Select(potion => potion.Id.ToString())
            .ToArray();
        return new CombatRewards(
            rewards.OfType<GoldReward>()
                .Where(reward => reward.IsResolved)
                .Sum(reward => reward.Amount),
            rewards.OfType<CardReward>()
                .SelectMany(reward => reward.Options)
                .Select(card => card.Id.ToString())
                .ToArray(),
            cardsTaken.Length == 1 ? cardsTaken[0] : null,
            relicsTaken.Length == 1 ? relicsTaken[0] : null,
            potionsTaken.Length == 1 ? potionsTaken[0] : null)
        {
            CardsTaken = cardsTaken,
            RelicsTaken = relicsTaken,
            PotionsTaken = potionsTaken,
        };
    }

    private void EnsureCombatStarted()
    {
        if (!_combatStarted)
        {
            throw new InvalidOperationException("Combat recording has not started.");
        }
    }

    private void EnsureTurnStarted()
    {
        EnsureCombatStarted();
        if (!_turnStarted)
        {
            throw new InvalidOperationException("Combat recording has no active player turn.");
        }
    }

    internal sealed record ActionSnapshot(
        PlayerSnapshot Player,
        IReadOnlyList<EnemySnapshot> Enemies,
        IReadOnlyList<string?> Potions);

    private sealed class CardActionContext
    {
        public CardActionContext(
            CardModel card,
            Creature? target,
            ActionSnapshot? before)
        {
            Card = card;
            Target = target;
            Before = before;
        }

        public CardModel Card { get; }

        public Creature? Target { get; }

        public ActionSnapshot? Before { get; }

        public List<DeferredPotionAction> DeferredPotions { get; } = [];
    }

    private sealed class EnemyActionContext
    {
        private ActionSnapshot _segmentStart;

        public EnemyActionContext(
            Creature source,
            string moveId,
            ActionSnapshot before)
        {
            Source = source;
            MoveId = moveId;
            _segmentStart = before;
        }

        public Creature Source { get; }

        public string MoveId { get; }

        public List<ActionSnapshotSegment> Segments { get; } = [];

        public List<DeferredPotionAction> DeferredPotions { get; } = [];

        public List<DamageResult> ResolvedDamage { get; } = [];

        public void CloseSegment(ActionSnapshot after)
        {
            Segments.Add(new ActionSnapshotSegment(
                _segmentStart.Player,
                after.Player,
                _segmentStart.Enemies,
                after.Enemies));
        }

        public void ResumeAfter(ActionSnapshot after)
        {
            _segmentStart = after;
        }
    }

    private sealed record PotionActionContext(
        PotionModel Potion,
        Creature? Target,
        ActionSnapshot Before,
        EnemyActionContext? EnemyOwner,
        CardActionContext? CardOwner);

    private sealed record DeferredPotionAction(
        PotionModel Potion,
        Creature? Target,
        ActionSnapshot Before,
        ActionSnapshot After,
        bool Consumed);
}
