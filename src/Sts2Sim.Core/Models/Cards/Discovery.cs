using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust，3选1随机生成卡免费进手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Discovery</c>），
/// 选中后设置本回合或首次打出前免费，再生成到手牌。</summary>
public sealed class Discovery : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ICombatState combatState = CombatState!;
        IReadOnlyList<CardModel> generated = CardFactory.GetDistinctForCombat(
            Owner,
            Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.Players.Count > 1),
            3,
            combatState.RunState.Rng.CombatCardGeneration);

        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(combatState, Owner, generated, 0, 1, this, cancelable: true)).FirstOrDefault();
        if (selected is not null)
        {
            selected.SetToFreeThisTurn();
            await CardPileCmd.Generate(combatState, selected, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
