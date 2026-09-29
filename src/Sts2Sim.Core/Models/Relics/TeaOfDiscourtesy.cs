using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class TeaOfDiscourtesy : RelicModel
{
    private int _combatsLeft = 1;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsUsedUp => _combatsLeft <= 0;

    public int CombatsLeft => Math.Max(0, _combatsLeft);

    public override async Task BeforeCombatStart()
    {
        if (IsUsedUp)
        {
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            var dazed = (Dazed)ModelDb.Card<Dazed>().MutableClone();
            dazed.AssignOwner(Owner);
            await CardPileCmd.Generate(
                Owner.Creature.CombatState!,
                dazed,
                PileType.Draw,
                CardPilePosition.Random);
        }

        _combatsLeft--;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_combatsLeft);
    }
}
