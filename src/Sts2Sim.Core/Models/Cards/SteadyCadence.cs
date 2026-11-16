using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>造成伤害并按本回合旋律种类数抽牌（原版 <c>SteadyCadence</c>）。</summary>
public sealed class SteadyCadence : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    private decimal Damage => IsUpgraded ? 13m : 10m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        int cardsToDraw = MelodyCmd.MelodiesThisTurn(Owner);
        if (cardsToDraw > 0)
        {
            await CardPileCmd.Draw(CombatState!, cardsToDraw, Owner, fromHandDraw: false);
        }
    }
}
