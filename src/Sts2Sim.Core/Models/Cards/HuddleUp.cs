using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;

namespace Sts2Sim.Core.Models.Cards;

public sealed class HuddleUp : GeneratedCardModel
{
    public override bool IsMultiplayerOnly => true;

    protected override GeneratedCardSpec Spec { get; } = new(
        1, 0, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies,
        true, false, false, new[] { CardKeyword.Exhaust },
        0m, 0, 0m, 2, 0, 0,
        0m, 0m, 0m, 0m, 0m,
        0m, 0m, 1, 0,
        0m, 0m, false, null, null);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int amount = Spec.Draw + (IsUpgraded ? Spec.UpgradeDraw : 0);
        foreach (Creature ally in CombatState!.Allies.Where(creature => creature.IsAlive && creature.IsPlayer))
        {
            await CardPileCmd.Draw(CombatState, amount, ally.Player!, false);
        }
    }
}