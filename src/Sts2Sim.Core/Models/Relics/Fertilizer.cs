using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>战斗内由持有者生成 Sadness 时获得 1 点力量（原版 <c>Fertilizer</c>）。</summary>
public sealed class Fertilizer : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator == Owner && card is Sadness && Owner.Creature.CombatState is { } combatState)
        {
            Flash();
            await PowerCmd.Apply<StrengthPower>(
                combatState, Owner.Creature, 1m, Owner.Creature, cardSource: null);
        }
    }
}
