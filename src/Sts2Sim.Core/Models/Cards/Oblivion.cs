using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Oblivion</c>：给目标施加 3（升级 4）层 <see cref="OblivionPower"/>。</summary>
public sealed class Oblivion : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    private decimal Doom => IsUpgraded ? 4m : 3m;

    // Doom 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        return PowerCmd.Apply<OblivionPower>(CombatState!, cardPlay.Target, Doom, Owner.Creature, this);
    }
}
