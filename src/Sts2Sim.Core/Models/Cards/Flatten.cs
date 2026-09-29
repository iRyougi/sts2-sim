using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Flatten</c>：Osty 对目标造成 12（升级 16）伤害；Osty 本回合攻击过时这张牌本回合费用为 0
/// （原版 <c>EnergyCost.SetThisTurn(0)</c>：回合结束才清除，打出后不清除）。Osty 不在时无效果。</summary>
public sealed class Flatten : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 2;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal OstyDamage => IsUpgraded ? 16m : 12m;

    // OstyDamage 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = OstyDamage;
        return true;
    }

    private bool HasOstyAttackedThisTurn =>
        CombatState is CombatState state &&
        state.SemanticHistory.CountThisTurn(state, CombatSemanticHistory.ActorEvent.OstyAttack, Owner) > 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (Owner.IsOstyMissing)
            return;

        await DamageCmd.Attack(OstyDamage).FromOsty(Owner.Osty!, this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card == this && HasOstyAttackedThisTurn)
            ReduceCost();
        return Task.CompletedTask;
    }

    public override Task AfterAttack(AttackCommand command)
    {
        if (command.Attacker is not null && command.Attacker == Owner.Osty)
            ReduceCost();
        return Task.CompletedTask;
    }

    private void ReduceCost() => SetTemporaryCostOverrideThisTurn(0);
}
