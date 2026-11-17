using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>抽牌前将对应张数随机塔罗牌加入手牌，按已升级张数升级（原版 <c>MythicFormPower</c>）。</summary>
public sealed class MythicFormPower : PowerModel
{
    private int _upgradedCount;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public int UpgradedCount => _upgradedCount;

    public void IncrementUpgraded()
    {
        AssertMutable();
        _upgradedCount++;
    }

    public override async Task BeforeHandDraw(Player player)
    {
        if (!ReferenceEquals(player, Owner.Player))
            return;

        ICombatState combatState = Owner.CombatState!;
        for (int i = 0; i < Amount; i++)
        {
            CardModel? canonical = player.RunState.Rng.CombatCardSelection.NextItem(
                TarotCards.All.Where(c => c.IsAllowedInRun(player.RunState)));
            if (canonical is null)
                continue;

            var card = (CardModel)canonical.MutableClone();
            card.AssignOwner(player);
            if (i < _upgradedCount && card.IsUpgradable)
            {
                card.Upgrade();
            }

            await CardPileCmd.Generate(combatState, card, PileType.Hand, player);
        }
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Entities.Creatures.Creature, Entities.Creatures.Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        _upgradedCount = ((MythicFormPower)source)._upgradedCount;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        builder.Append(_upgradedCount);
}
