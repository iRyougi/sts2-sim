namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;

public sealed class SurprisePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterDeath(Creature target)
    {
        if (target != Owner || Owner.CombatState is not { } combatState ||
            !combatState.IsLiveCombat())
        {
            return;
        }

        var thefts = Owner.Powers.OfType<ThieveryPower>().ToArray();
        Creature fat = combatState.CreateCreature(
            (FatGremlin)ModelDb.Monster<FatGremlin>().MutableClone(), CombatSide.Enemy, "fat");
        foreach (ThieveryPower theft in thefts)
        {
            HeistPower? heist = await PowerCmd.Apply<HeistPower>(
                combatState,
                fat,
                theft.GoldStolen,
                Owner,
                null);
            if (heist is not null)
            {
                heist.Target = theft.Target;
            }
        }

        var sneaky = (SneakyGremlin)ModelDb.Monster<SneakyGremlin>().MutableClone();
        await CreatureCmd.Add(sneaky, combatState, CombatSide.Enemy, "sneaky");
        await CreatureCmd.Add(fat);

        if (thefts.Sum(theft => theft.GoldStolen) > 0 &&
            combatState is CombatState state &&
            state.RunState.CurrentRoom is CombatRoom { EncounterName: "GremlinMercNormal" })
        {
            state.MarkGoldStolen();
        }
    }
    public override bool ShouldStopCombatFromEnding() => true;
}
