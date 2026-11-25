using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>0 费、消耗、保留：拔出目标身上自己的全部箭矢；升级去掉消耗（v0.114.0 原版 <c>Recall</c>）。</summary>
public sealed class Recall : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust, CardKeyword.Retain];

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override bool ShouldGlowGoldInternal => CombatState?.HittableEnemies.Any(c => ArrowCmd.CreatureIsImpaledByPlayersArrow(c, Owner)) ?? false;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (ArrowCmd.CreatureIsImpaledByPlayersArrow(cardPlay.Target, Owner))
        {
            while (ArrowCmd.CreatureIsImpaledByPlayersArrow(cardPlay.Target, Owner))
            {
                await ArrowCmd.DislodgeArrow(cardPlay.Target, cardPlay.Player, this);
            }
        }
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
