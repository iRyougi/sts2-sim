using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>10伤害,抽1张,放回1张手牌到堆顶。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.PhotonCut</c>）</summary>
public sealed class PhotonCut : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 10m;
    private int _draw = 1;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await CardPileCmd.Draw(CombatState!, _draw, Owner, fromHandDraw: false);
        CardModel? selected = (await CardSelectCmd.FromHand(CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards, 1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is not null)
        {
            CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
        }
    }

    protected override void OnUpgrade()
    {
        _damage += 3m;
        _draw += 1;
    }
}
