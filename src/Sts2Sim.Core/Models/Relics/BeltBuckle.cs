using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BeltBuckle : RelicModel
{
    private bool _dexterityApplied;

    public override RelicRarity Rarity => RelicRarity.Shop;

    public override Task AfterObtained() => ReconcileDexterity();

    public override Task BeforeCombatStart()
    {
        _dexterityApplied = false;
        RefreshStatus();
        return ReconcileDexterity();
    }

    public override Task AfterPotionUsed(PotionModel potion, Player player)
    {
        RefreshStatus();
        return player == Owner ? ReconcileDexterity() : Task.CompletedTask;
    }

    public override Task AfterPotionProcured(PotionModel potion)
    {
        RefreshStatus();
        return ReconcileDexterity();
    }

    public override Task AfterPotionDiscarded(PotionModel potion)
    {
        RefreshStatus();
        return ReconcileDexterity();
    }

    public override Task AfterCombatVictory()
    {
        // Source AfterCombatVictory also clears DexterityApplied; that is already done in AfterCombatEnd here.
        RefreshStatus();
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        _dexterityApplied = false;
        RefreshStatus();
        return Task.CompletedTask;
    }

    // Source RefreshStatus: Active only while a combat is in progress and the owner holds no potion.
    private void RefreshStatus()
    {
        if (Owner.Creature.CombatState is { } combatState && combatState.IsLiveCombat() &&
            !Owner.PotionSlots.Any(potion => potion is not null))
        {
            Status = RelicStatus.Active;
        }
        else
        {
            Status = RelicStatus.Normal;
        }
    }

    private async Task ReconcileDexterity()
    {
        if (Owner.Creature.CombatState is not { } combatState || !combatState.IsLiveCombat())
        {
            return;
        }

        bool hasPotion = Owner.PotionSlots.Any(potion => potion is not null);
        if (!hasPotion)
        {
            if (_dexterityApplied)
            {
                return;
            }

            _dexterityApplied = true;
            await PowerCmd.Apply<DexterityPower>(
                combatState,
                Owner.Creature,
                2m,
                applier: null,
                cardSource: null);
            return;
        }

        if (!_dexterityApplied)
        {
            return;
        }

        _dexterityApplied = false;
        await PowerCmd.Apply<DexterityPower>(
            combatState,
            Owner.Creature,
            -2m,
            applier: null,
            cardSource: null);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_dexterityApplied);
    }
}
