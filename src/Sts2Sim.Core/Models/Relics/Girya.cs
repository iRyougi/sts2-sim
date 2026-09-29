using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.RestSite;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Girya : RelicModel
{
    private int _timesLifted;
    public int TimesLifted
    {
        get => _timesLifted;
        set { AssertMutable(); _timesLifted = value; }
    }

    public override RelicRarity Rarity => RelicRarity.Rare;
    public override bool IsAllowed(IRunState runState) => IsBeforeAct3TreasureChest(runState);

    public override void ModifyAvailableRestSiteDecisions(IRunState runState, Player player, List<RestSiteDecision> decisions)
    {
        if (ReferenceEquals(player, Owner) && TimesLifted < 3)
        {
            decisions.Add(new LiftRestSiteOption());
        }
    }

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (TimesLifted > 0 && room is CombatRoom combatRoom)
        {
            await PowerCmd.Apply<StrengthPower>(combatRoom.Engine.State, Owner.Creature,
                TimesLifted, Owner.Creature, cardSource: null);
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(TimesLifted);
}
