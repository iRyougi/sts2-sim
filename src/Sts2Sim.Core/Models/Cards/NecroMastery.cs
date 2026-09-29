using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>NecroMastery</c>：召唤 5（升级 8），再获得 1 层 <see cref="NecroMasteryPower"/>。</summary>
public sealed class NecroMastery : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 2;

    private decimal Summon => IsUpgraded ? 8m : 5m;

    // Summon 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await OstyCmd.Summon(Owner, Summon, this);
        await PowerCmd.Apply<NecroMasteryPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }
}
