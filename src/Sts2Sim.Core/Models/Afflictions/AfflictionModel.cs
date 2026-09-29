namespace Sts2Sim.Core.Models.Afflictions;

using Sts2Sim.Core.Entities.Cards;

/// <summary>
/// Combat-only attachment owned by one mutable card.
/// 偏离 #198：保留 Hive 所需的附着、叠层、clone、入战与打牌 hook 语义；无头模拟器尚无
/// affliction 本地化/overlay/history、全局 ShouldAfflict veto 与数值重算通知表面。
/// </summary>
public abstract class AfflictionModel : AbstractModel
{
    private CardModel? _card;

    public CardModel Card => _card ?? throw new InvalidOperationException("Affliction is not attached to a card.");
    public bool HasCard => _card is not null;
    public int Amount { get; private set; }
    public override bool ShouldReceiveCombatHooks => HasCard && Card.ShouldReceiveCombatHooks;
    public virtual bool IsStackable => false;
    public virtual bool CanAfflictCardType(CardType cardType) => true;
    public virtual bool CanAfflict(CardModel card) =>
        CanAfflictCardType(card.Type) &&
        (card.Affliction is null || (IsStackable && card.Affliction.GetType() == GetType()));

    /// <summary>Allows combat-only afflictions to add effective card keywords without mutating the card's base keywords.</summary>
    public virtual bool TryModifyKeywords(ISet<CardKeyword> keywords) => false;

    internal void Attach(CardModel card, decimal amount)
    {
        AssertMutable();
        card.AssertMutable();
        if (_card is not null) throw new InvalidOperationException("Affliction is already attached to a card.");
        _card = card;
        SetAmount(amount);
    }

    internal void SetAmount(decimal amount)
    {
        AssertMutable();
        Amount = checked((int)amount);
    }

    internal void Clear()
    {
        AssertMutable();
        _card = null;
    }

    public virtual Task OnPlay(CardPlay cardPlay) => Task.CompletedTask;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _card = null;
    }
}
