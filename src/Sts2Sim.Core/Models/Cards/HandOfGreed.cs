using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>20 伤害；只有目标允许 Fatal 且本次伤害击杀目标时获得 20 金币。</summary>
public sealed class HandOfGreed : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 20m;
    private int _gold = 20;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    public override bool IsColorless => true;

    public override bool CanBeGeneratedInCombat => false;

    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool shouldTriggerFatal = cardPlay.Target.Powers.All(power => power.ShouldOwnerDeathTriggerFatal());
        AttackCommand attack = await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        if (shouldTriggerFatal && attack.Results.SelectMany(hits => hits).Any((DamageResult r) => r.WasTargetKilled))
        {
            await PlayerCmd.GainGold(_gold, Owner);
        }
    }

    protected override void OnUpgrade()
    {
        _damage += 5m;
        _gold += 5;
    }
}
