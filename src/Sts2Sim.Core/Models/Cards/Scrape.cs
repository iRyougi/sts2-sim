using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Scrape : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 7m;
    private int _cards = 4;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage, Cards: _cards);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        IReadOnlyList<CardModel> drawn = await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
        await CardCmd.Discard(drawn.Where(card => card.EnergyCost != 0 || card.CostsXEnergy));
    }

    protected override void OnUpgrade() { _damage += 3m; _cards += 1; }
}
