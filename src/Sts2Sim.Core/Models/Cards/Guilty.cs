using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Guilty : CardModel, ICardComparisonBaseValueSource
{
    /// <summary>v0.113.1 CompareTo 视图：原版 <c>Combats</c> 的当前 BaseValue。</summary>
    public decimal GetComparisonBaseValue(string key) => key == "Combats"
        ? 5m - _combatsCompleted
        : throw new ArgumentOutOfRangeException(nameof(key), key, "Not a runtime-mutable comparison key.");

    private int _combatsCompleted;

    public override CardType Type => CardType.Curse;
    public override CardRarity Rarity => CardRarity.Curse;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => -1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };

    public override Task AfterCombatEnd()
    {
        _combatsCompleted++;
        if (_combatsCompleted >= 5 && Pile?.Type == PileType.Deck)
        {
            Commands.CardPileCmd.Remove(this);
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_combatsCompleted);
    }
}
