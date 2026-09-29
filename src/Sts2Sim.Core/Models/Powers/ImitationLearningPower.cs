using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ImitationLearningPower : PowerModel
{
    private Player? _playerTarget;
    private List<(CardModel Original, CardModel Clone)> _pending = [];
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public Player PlayerTarget
    {
        get => _playerTarget ?? throw new InvalidOperationException("Target player has not been assigned.");
        set { AssertMutable(); _playerTarget = value; }
    }

    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (_playerTarget is null)
            throw new InvalidOperationException("Imitation Learning has no target player.");
        if (play.Card.Owner != _playerTarget || play.Card.Type != CardType.Power || !play.IsFirstInSeries)
            return Task.CompletedTask;
        CardModel clone = play.Card.CreateClone();
        clone.AssignOwner(Owner.Player!);
        _pending.Add((play.Card, clone));
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay play)
    {
        int index = _pending.FindIndex(item => ReferenceEquals(item.Original, play.Card));
        if (index < 0) return;
        CardModel clone = _pending[index].Clone;
        _pending.RemoveAt(index);
        await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        await AutoPlayCmd.FromCards(Owner.CombatState!, Owner.Player!, [clone]);
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _pending = _pending.ToList();
    }

    internal override IEnumerable<CardModel> EnumerateCombatCloneCards() =>
        _pending.Select(item => item.Clone);

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        var original = (ImitationLearningPower)source;
        _playerTarget = original._playerTarget is null ? null : creatureMap[original._playerTarget.Creature].Player;
        _pending = original._pending.Select(item => (
            cardMap.TryGetValue(item.Original, out CardModel? mappedOriginal) ? mappedOriginal : item.Original,
            cardMap.TryGetValue(item.Clone, out CardModel? mappedClone) ? mappedClone : item.Clone.CloneForCombat(Owner.Player)
        )).ToList();
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context)
    {
        builder.Append(_playerTarget is not null);
        if (_playerTarget is not null)
            builder.Append(context.State.Players.ToList().IndexOf(_playerTarget));
        builder.Append(_pending.Count);
        foreach (var item in _pending)
        {
            context.AppendCardCloneOrigin(ref builder, item.Original);
            context.AppendDetachedCard(ref builder, item.Clone);
        }
    }
}
