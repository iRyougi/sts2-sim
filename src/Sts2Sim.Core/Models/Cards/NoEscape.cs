using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>NoEscape</c>：给目标施加 10（升级 15）+ 5 × ⌊目标当前 Doom / 10⌋ 的 <see cref="DoomPower"/>。</summary>
public sealed class NoEscape : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    private const decimal DoomThreshold = 10m;

    private decimal CalculationBase => IsUpgraded ? 15m : 10m;

    private const decimal CalculationExtra = 5m;

    // DoomThreshold/CalculationBase/CalculationExtra/CalculatedDoom 都不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int targetDoom = cardPlay.Target.GetPower<DoomPower>()?.Amount ?? 0;
        decimal doom = CalculationBase + CalculationExtra * Math.Floor(targetDoom / DoomThreshold);
        await PowerCmd.Apply<DoomPower>(CombatState!, cardPlay.Target, doom, Owner.Creature, this);
    }
}
