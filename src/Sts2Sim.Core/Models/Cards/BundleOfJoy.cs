using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust，生成3张不同无色卡进手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.BundleOfJoy</c>）。</summary>
public sealed class BundleOfJoy : CardModel
{
    private int _count = 3;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ICombatState combatState = CombatState!;
        List<CardModel> candidates = ColorlessCardPool.Instance.GetUnlockedCards(
                Owner.UnlockState, Owner.RunState.Players.Count > 1)
            .ToList();

        foreach (CardModel clone in CardFactory.GetDistinctForCombat(
            Owner, candidates, _count, combatState.RunState.Rng.CombatCardGeneration))
        {
            await CardPileCmd.Generate(combatState, clone, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => _count += 1;
}
