using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Underworld</c>（仅限多人）：2 费、消耗（升级移除消耗），获得 1 层 <see cref="UnderworldPower"/>。</summary>
public sealed class Underworld : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsMultiplayerOnly => true;

    protected override int CanonicalEnergyCost => 2;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<UnderworldPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
