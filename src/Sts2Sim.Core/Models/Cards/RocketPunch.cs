using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Cards;

public sealed class RocketPunch : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 13m;
    private int _cards = 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage, Cards: _cards);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator == Owner && card.Owner == Owner && card.Type == CardType.Status)
            AddEnergyCostUntilPlayed(-1);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() { _damage += 1m; _cards += 1; }
}
