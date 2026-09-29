using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Sacrifice</c>：1 费（升级 0 费）、保留。Osty 存活时先按其最大生命×3 算好格挡，
/// 再击杀 Osty，最后获得格挡（Move，受敏捷等修正）。Osty 不在时无效果。</summary>
public sealed class Sacrifice : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

    public override bool GainsBlock => true;

    // CalculatedBlock 不是字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        if (Owner.IsOstyMissing)
            return;

        // 原版 CalculatedBlockVar：CalculationBase 0 + CalculationExtra 1 × (Osty 存活 ? MaxHp×3 : 0)。
        decimal blockGain = Owner.IsOstyAlive ? Owner.Osty!.MaxHp * 3 : 0m;
        await CreatureCmd.Kill(Owner.Osty!);
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, blockGain, ValueProp.Move, this, cardPlay);
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
