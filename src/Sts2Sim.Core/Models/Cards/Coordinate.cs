using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Coordinate : GeneratedCardModel
{
    public override bool IsMultiplayerOnly => true;

    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly,
        true, false, false, Array.Empty<CardKeyword>(),
        0m, 0, 0m, 0, 0, 0,
        0m, 0m, 5m, 0m, 0m,
        0m, 0m, 0, 0,
        0m, 0m, false, null, null, UpgradeStrength: 3m);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        Creature target = cardPlay.Target
            ?? throw new ArgumentNullException(nameof(cardPlay.Target));
        decimal amount = Spec.Strength + (IsUpgraded ? Spec.UpgradeStrength : 0m);
        await PowerCmd.Apply<CoordinatePower>(CombatState!, target, amount, Owner.Creature, this);
    }
}