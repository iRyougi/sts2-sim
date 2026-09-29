using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>8伤害,+2星愿;出牌后进抽牌堆顶。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.ShiningStrike</c>）。</summary>
public sealed class ShiningStrike : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 8m;
    private const decimal Stars = 2m;

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
        await PlayerCmd.GainStars(Stars, Owner);
    }

    protected override CardLocation GetResultLocationForCardPlay()
    {
        CardLocation location = base.GetResultLocationForCardPlay();
        return location.PileType == PileType.Discard
            ? location with { PileType = PileType.Draw, Position = CardPilePosition.Top }
            : location;
    }

    protected override void OnUpgrade() => _damage += 3m;
}
