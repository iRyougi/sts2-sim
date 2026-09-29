using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>8伤害;每次被抽到永久+4伤害(叠加)。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.KinglyPunch</c>）。</summary>
public sealed class KinglyPunch : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 8m;
    private decimal _increasePerDraw = 4m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    public override Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        if (card == this)
        {
            _damage += _increasePerDraw;
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        _damage += 2m;
        _increasePerDraw += 2m;
    }
}
