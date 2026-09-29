using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Prowess : GeneratedCardModel
{
    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Power, CardRarity.Uncommon, TargetType.Self,
        true, false, false, Array.Empty<CardKeyword>(),
        0m, 1, 0m, 0, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal amount = IsUpgraded ? 2m : 1m;
        await PowerCmd.Apply<StrengthPower>(
            CombatState!,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);
        await PowerCmd.Apply<DexterityPower>(
            CombatState!,
            Owner.Creature,
            amount,
            Owner.Creature,
            this);
    }
}
