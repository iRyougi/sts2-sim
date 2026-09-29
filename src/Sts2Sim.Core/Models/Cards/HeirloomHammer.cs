using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>20伤害，复制1张手牌中的无色卡进手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.HeirloomHammer</c>），
/// 选择经过选牌决策源。</summary>
public sealed class HeirloomHammer : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 20m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        ICombatState combatState = CombatState!;
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();

        CardModel? selected = (await CardSelectCmd.FromHand(
            CombatState!, Owner, Owner.PlayerCombatState!.Hand.Cards.Where(CardPoolFilters.IsVisuallyColorless),
            1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is not null)
        {
            var clone = (CardModel)selected.MutableClone();
            await CardPileCmd.Generate(combatState, clone, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => _damage += 5m;
}
