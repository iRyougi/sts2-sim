using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Begone/Charge 转化生成的随从卡。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.MinionStrike</c>）：
/// 0费,造成6点伤害并抽1张牌,Exhaust。</summary>
public sealed class MinionStrike : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 6m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await CardPileCmd.Draw(CombatState!, 1, Owner, fromHandDraw: false);
    }

    protected override void OnUpgrade() => _damage += 3m;
}
