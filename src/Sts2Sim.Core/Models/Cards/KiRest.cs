using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>下回合获得能量；若本回合打出过 Wah 或 Cho 则保留手牌（原版 <c>KiRest</c>）。</summary>
public sealed class KiRest : CardModel, ICardChoiceBaseValueProvider
{
    protected override bool ShouldGlowGoldInternal => MelodyCmd.PlayedWahThisTurn(Owner) || MelodyCmd.PlayedChoThisTurn(Owner);

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Ki];
    private int Energy => IsUpgraded ? 5 : 4;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: Energy);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<EnergyNextTurnPower>(CombatState!, Owner.Creature, Energy, Owner.Creature, this);
        if (MelodyCmd.PlayedWahThisTurn(Owner) || MelodyCmd.PlayedChoThisTurn(Owner))
        {
            await PowerCmd.Apply<RetainHandPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
        }
    }
}
