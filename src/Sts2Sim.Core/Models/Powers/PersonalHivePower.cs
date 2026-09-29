namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.ValueProps;

public sealed class PersonalHivePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        // 原版：Osty 发起的攻击，Dazed 塞给它的主人。
        if (dealer?.Monster is Models.Monsters.Osty)
        {
            dealer = dealer.PetOwner?.Creature;
        }

        if (!ReferenceEquals(target, Owner) || dealer?.Player is null || !props.IsPoweredAttack())
        {
            return;
        }

        for (int index = 0; index < Amount; index++)
        {
            var dazed = (Dazed)ModelDb.Card<Dazed>().MutableClone();
            dazed.AssignOwner(dealer.Player);
            await CardPileCmd.Generate(
                Owner.CombatState!,
                dazed,
                PileType.Draw,
                creator: null,
                CardPilePosition.Random);
        }
    }
}
