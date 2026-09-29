using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Feed : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);

    private decimal _damage = 10m;
    private decimal _maxHp = 3m;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool canTriggerFatal = cardPlay.Target.Powers.All(power => power.ShouldOwnerDeathTriggerFatal());
        AttackCommand attack = await DamageCmd.Attack(_damage)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        if (canTriggerFatal && attack.Results.SelectMany(hit => hit).Any(result => result.WasTargetKilled))
        {
            await CreatureCmd.GainMaxHp(Owner.Creature, _maxHp);
        }
    }

    protected override void OnUpgrade()
    {
        _damage += 2m;
        _maxHp += 1m;
    }
}
