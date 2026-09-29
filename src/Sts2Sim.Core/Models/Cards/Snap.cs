using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Snap</c>：1 费 Osty 攻击 7（升级 10）；随后无论 Osty 是否在场，都从手牌选 1 张
/// 不带保留的牌使其获得保留。</summary>
public sealed class Snap : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal OstyDamage => IsUpgraded ? 10m : 7m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = OstyDamage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (!Owner.IsOstyMissing)
        {
            await DamageCmd.Attack(OstyDamage).FromOsty(Owner.Osty!, this, cardPlay)
                .Targeting(cardPlay.Target).Execute();
        }

        // 原版过滤读 Keywords（本地+全局），单回合保留不在其中。
        CardModel? selected = (await CardSelectCmd.FromHand(CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards.Where(card => !card.Keywords.Contains(CardKeyword.Retain)),
            1, 1, this)).FirstOrDefault();
        selected?.AddKeywordInternal(CardKeyword.Retain);
    }
}
