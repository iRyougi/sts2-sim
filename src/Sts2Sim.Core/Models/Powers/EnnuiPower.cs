using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>抽牌前向手牌生成对应张数 Sadness，并额外抽取叠加层数张牌（原版 <c>EnnuiPower</c>）。</summary>
public sealed class EnnuiPower : PowerModel
{
    private int _sadnessAmount;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public int SadnessAmount => _sadnessAmount;

    public void IncrementSadnessAmount()
    {
        AssertMutable();
        _sadnessAmount++;
    }

    public override async Task BeforeHandDraw(Player player)
    {
        if (!ReferenceEquals(player, Owner.Player))
            return;

        ICombatState combatState = Owner.CombatState!;
        for (int i = 0; i < _sadnessAmount; i++)
        {
            var sadness = (CardModel)ModelDb.Card<Sadness>().MutableClone();
            sadness.AssignOwner(player);
            await CardPileCmd.Generate(combatState, sadness, PileType.Hand, player);
        }
    }

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        ReferenceEquals(player, Owner.Player) ? count + Amount : count;

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Entities.Creatures.Creature, Entities.Creatures.Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        _sadnessAmount = ((EnnuiPower)source)._sadnessAmount;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        builder.Append(_sadnessAmount);
}
