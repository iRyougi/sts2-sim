using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>本场战斗每生成 1 张 Sadness 即增伤 6（升级 7）的攻击（原版 <c>Anguish</c>）。</summary>
public sealed class Anguish : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    private decimal CalculationBase => IsUpgraded ? 7m : 6m;
    private decimal ExtraDamage => IsUpgraded ? 7m : 6m;
    private int SadnessGenerated => HasOwner && Owner.PlayerCombatState is { } pcs ? pcs.SadnessGeneratedThisCombat : 0;
    private decimal CalculatedDamage => CalculationBase + ExtraDamage * SadnessGenerated;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = CalculatedDamage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(CalculatedDamage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
}
