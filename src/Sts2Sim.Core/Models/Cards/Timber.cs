using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>造成 20（升级 25）点伤害；若击杀目标则对全体敌人再造成 30（升级 35）点伤害（原版 <c>Timber</c>）。</summary>
public sealed class Timber : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 3;
    private decimal Damage => IsUpgraded ? 25m : 20m;
    private decimal OnKillDamage => IsUpgraded ? 35m : 30m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var attack = await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        if (attack.Results.SelectMany(r => r).Any(r => r.WasTargetKilled))
        {
            await DamageCmd.Attack(OnKillDamage).FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();
        }
    }
}
