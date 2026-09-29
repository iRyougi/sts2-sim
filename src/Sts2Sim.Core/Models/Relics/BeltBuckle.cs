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
        return ReconcileDexterity();
    }

    public override Task AfterPotionUsed(PotionModel potion, Player player)
    {
        return player == Owner ? ReconcileDexterity() : Task.CompletedTask;
    }

    public override Task AfterPotionProcured(PotionModel potion) => ReconcileDexterity();
    public override Task AfterPotionDiscarded(PotionModel potion) => ReconcileDexterity();

    public override Task AfterCombatEnd()
    {
        _dexterityApplied = false;
        return Task.CompletedTask;
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
