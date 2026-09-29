using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

public sealed class SlipperyBridge : EventModel
{
    private HashSet<CardModel> _skipped = [];
    private CardModel? _randomCard;
    private int _holdOns;
    public CardModel RandomCardToLose => _randomCard ?? throw new InvalidOperationException("Event has not begun.");
    public int CurrentHpLoss => 3 + _holdOns;

    public override bool IsAllowed(IRunState runState) => runState.TotalFloor > 6 &&
        runState.Players.All(p => p.Deck.Cards.Any(c => c.IsRemovable));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        PickCard();
        return Options();
    }

    private IReadOnlyList<EventOption> Options() =>
    [
        new("OVERCOME", Overcome),
        new("HOLD_ON_" + (_holdOns >= 7 ? "LOOP" : _holdOns.ToString()), HoldOn),
    ];

    private void PickCard()
    {
        if (_randomCard is not null) _skipped.Add(_randomCard);
        var candidates = Owner.Deck.Cards.Where(c => c.IsRemovable && !_skipped.Contains(c) &&
            (_randomCard is null ? c.Rarity != CardRarity.Basic : c.GetType() != _randomCard.GetType())).ToList();
        if (candidates.Count == 0) candidates = Owner.Deck.Cards.Where(c => c.IsRemovable).ToList();
        _randomCard = Rng.NextItem(candidates) ?? throw new InvalidOperationException("No removable card remains.");
    }

    private async Task Overcome()
    {
        await CardPileCmd.RemoveFromDeck(Owner, RandomCardToLose);
        Finish();
    }

    private async Task HoldOn()
    {
        await CreatureCmd.Damage(RunState, Owner.Creature, CurrentHpLoss, ValueProp.Unblockable | ValueProp.Unpowered);
        _holdOns++;
        PickCard();
        SetOptions(Options());
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _skipped = [];
        _randomCard = null;
        _holdOns = 0;
    }
}
