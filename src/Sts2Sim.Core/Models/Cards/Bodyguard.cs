using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Bodyguard</c>：亡灵契约师起始技能，1 费召唤 5（升级 7）。</summary>
public sealed class Bodyguard : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Basic;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    private decimal Summon => IsUpgraded ? 7m : 5m;

    // Summon 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) => OstyCmd.Summon(Owner, Summon, this);
}
