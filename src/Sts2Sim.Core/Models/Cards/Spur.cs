using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Spur</c>：1 费、保留，召唤 3（升级 5），然后为 Osty 回复 5（升级 7）生命。</summary>
public sealed class Spur : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    private decimal Summon => IsUpgraded ? 5m : 3m;

    private decimal Heal => IsUpgraded ? 7m : 5m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await OstyCmd.Summon(Owner, Summon, this);
        // 原版直接对 Owner.Osty 回复；只有召唤量被钩子改为 0 且从未召唤过时 Osty 才为空，原版此时会抛异常。
        if (Owner.Osty is { } osty)
            await CreatureCmd.Heal(osty, Heal);
    }
}
