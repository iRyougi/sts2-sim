using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class PhantomBladesPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        foreach (CardModel card in Owner.Player!.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).Where(card => card.Tags.Contains(CardTag.Shiv)))
            card.AddKeywordInternal(CardKeyword.Retain);
        return Task.CompletedTask;
    }
    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card.Owner == Owner.Player && card.Tags.Contains(CardTag.Shiv)) card.AddKeywordInternal(CardKeyword.Retain);
        return Task.CompletedTask;
    }
    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!props.IsPoweredAttack() || cardSource?.Tags.Contains(CardTag.Shiv) != true || dealer != Owner)
        {
            return 0m;
        }

        return Owner.Player!.PlayerCombatState!.ShivsPlayedThisTurn == 0 ? Amount : 0m;
    }
}
