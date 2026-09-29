using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Reave</c>：1 费，造成 10（升级 13）伤害，然后把 1 张 <see cref="Soul"/>
/// 随机洗入抽牌堆；升级版的 Soul 也先升级再入堆。</summary>
public sealed class Reave : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const int SoulCount = 1;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    private decimal Damage => IsUpgraded ? 13m : 10m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Cards: SoulCount);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();

        // 原版 Soul.Create 先一次性建好全部 Soul，升级后再逐张入堆（每张各取一次 Shuffle 流的随机位置）。
        List<Soul> souls = Soul.Create(Owner, SoulCount);

        if (IsUpgraded)
        {
            foreach (Soul soul in souls)
            {
                CardCmd.Upgrade(soul);
            }
        }

        foreach (Soul soul in souls)
        {
            await CardPileCmd.Generate(CombatState!, soul, PileType.Draw, Owner, CardPilePosition.Random);
        }
    }
}
