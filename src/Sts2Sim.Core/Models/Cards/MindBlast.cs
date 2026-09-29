using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class MindBlast : GeneratedCardModel
{
    public override bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Owner.PlayerCombatState!.DrawPile.Cards.Count;
        return true;
    }

    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy,
        true, false, false, new[] { CardKeyword.Innate },
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, true, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Owner.PlayerCombatState!.DrawPile.Cards.Count)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();
    }
}
