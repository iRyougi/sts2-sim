using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Entities.RestSite;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>
/// Deviation #160 is now presentation-only: Godot skin, VFX, and ByrdSwoop attacker animation are omitted.
/// The pet creature, summon timing, and egg transformations follow the native behavior.
/// v0.113.1 (#928): <see cref="SwoopsPlayed"/> counts the owner's ByrdSwoop plays (capped at
/// <see cref="PlaysToEvolve"/>); once reached, the rest site offers EVOLVE. Counter display/status icon omitted (#160).
/// </summary>
public sealed class Byrdpip : RelicModel
{
    public const int PlaysToEvolve = 6;

    private int _swoopsPlayed;

    public int SwoopsPlayed
    {
        get => _swoopsPlayed;
        set
        {
            AssertMutable();
            _swoopsPlayed = value;
        }
    }

    public override RelicRarity Rarity => RelicRarity.Event;
    public override bool AddsPet => true;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        var eggs = Owner.Deck.Cards.OfType<ByrdonisEgg>().Cast<CardModel>().ToList();
        if (Owner.Creature.CombatState is { } combatState &&
            combatState.IsLiveCombat() &&
            Owner.PlayerCombatState is { } playerCombatState)
        {
            eggs.AddRange(playerCombatState.AllPiles
                .SelectMany(pile => pile.Cards)
                .OfType<ByrdonisEgg>());
        }

        foreach (CardModel egg in eggs)
        {
            await CardCmd.CreateAndTransform<ByrdSwoop>(egg);
        }

        if (Owner.Creature.CombatState?.IsLiveCombat() == true)
        {
            await SummonPet();
        }
    }

    public override Task BeforeCombatStart() => SummonPet();

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card is not ByrdSwoop) return Task.CompletedTask;
        if (cardPlay.Card.Owner != Owner) return Task.CompletedTask;
        if (SwoopsPlayed >= PlaysToEvolve) return Task.CompletedTask;
        SwoopsPlayed++;
        return Task.CompletedTask;
    }

    public override void ModifyAvailableRestSiteDecisions(IRunState runState, Player player, List<RestSiteDecision> decisions)
    {
        if (!ReferenceEquals(player, Owner)) return;
        if (SwoopsPlayed < PlaysToEvolve) return;
        decisions.Add(new EvolveRestSiteOption());
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) => builder.Append(SwoopsPlayed);

    private async Task SummonPet()
    {
        await PlayerCmd.AddPet<global::Sts2Sim.Core.Models.Monsters.Byrdpip>(Owner);
    }
}
