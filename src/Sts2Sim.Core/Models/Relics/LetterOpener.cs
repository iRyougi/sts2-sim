using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LetterOpener : RelicModel
{
    private int _skillsThisTurn;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeCombatStart()
    {
        _skillsThisTurn = 0;
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature) &&
            Owner.PlayerCombatState?.TurnNumber != 1)
        {
            _skillsThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner ||
            cardPlay.Card.Type != CardType.Skill)
        {
            return;
        }

        _skillsThisTurn++;
        if (_skillsThisTurn % 3 != 0)
        {
            return;
        }

        ICombatState combatState = Owner.Creature.CombatState!;
        await CreatureCmd.Damage(
            combatState,
            combatState.HittableEnemies.ToArray(),
            5m,
            ValueProp.Unpowered,
            Owner.Creature,
            null,
            cardPlay);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_skillsThisTurn);
    }
}
