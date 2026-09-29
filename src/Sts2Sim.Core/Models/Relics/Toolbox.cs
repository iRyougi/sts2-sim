using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Toolbox : RelicModel
{
    private bool _wasUsedThisCombat;

    public override RelicRarity Rarity => RelicRarity.Shop;

    public override Task BeforeCombatStart()
    {
        _wasUsedThisCombat = false;
        return Task.CompletedTask;
    }

    public override async Task BeforeHandDraw(Player player)
    {
        if (_wasUsedThisCombat || player != Owner || Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        _wasUsedThisCombat = true;
        ICombatState combatState = Owner.Creature.CombatState!;
        List<CardModel> candidates = ColorlessCardPool.Instance.GetUnlockedCards(
                Owner.UnlockState, Owner.RunState.Players.Count > 1)
            .ToList();
        IReadOnlyList<CardModel> choices = CardFactory.GetDistinctForCombat(
            Owner, candidates, 3, combatState.RunState.Rng.CombatCardGeneration);

        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(combatState, Owner, choices, 1, 1, this)).FirstOrDefault();
        if (selected is not null)
        {
            await CardPileCmd.Generate(combatState, selected, PileType.Hand);
        }
    }

    public override Task AfterCombatEnd()
    {
        _wasUsedThisCombat = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_wasUsedThisCombat);
    }
}
