using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>
/// Deviation #160 is now presentation-only: Godot skin, VFX, and ByrdSwoop attacker animation are omitted.
/// The pet creature, summon timing, and egg transformations follow the native behavior.
/// </summary>
public sealed class Byrdpip : RelicModel
{
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

    private async Task SummonPet()
    {
        await PlayerCmd.AddPet<global::Sts2Sim.Core.Models.Monsters.Byrdpip>(Owner);
    }
}
