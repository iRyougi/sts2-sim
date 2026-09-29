using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Ftl : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 5m;
    private int _playMax = 3;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage, Cards: 1);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }

    protected override async Task OnPlay(CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(_damage).FromCard(this, play).Targeting(play.Target).Execute();
        // Completed plays are recorded after OnPlay, matching CardPlaysFinished at this point.
        if (Owner.PlayerCombatState!.CardsPlayedThisTurn < _playMax)
            await CardPileCmd.Draw(CombatState!, 1, Owner, fromHandDraw: false);
    }

    protected override void OnUpgrade() { _damage += 1m; _playMax += 1; }
}
