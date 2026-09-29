using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class CrescentSpear : GeneratedCardModel
{
    public override bool TryGetThrashDamageVariable(out decimal amount)
    {
        int starCardCount = Owner.PlayerCombatState!.AllPiles
            .SelectMany(pile => pile.Cards)
            .Count(card => card.HasStarCost);
        amount = 8m + (IsUpgraded ? 3m : 2m) * starCardCount;
        return true;
    }

    protected override GeneratedCardSpec Spec { get; } = new(
        1, 1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy,
        false, false, false, Array.Empty<CardKeyword>(),
        8m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int starCardCount = Owner.PlayerCombatState!.AllPiles
            .SelectMany(pile => pile.Cards)
            .Count(card => card.HasStarCost);
        decimal damagePerStarCard = IsUpgraded ? 3m : 2m;
        await DamageCmd.Attack(8m + damagePerStarCard * starCardCount)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();
    }
}
