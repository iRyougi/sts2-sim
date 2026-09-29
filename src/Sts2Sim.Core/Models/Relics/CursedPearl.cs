using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class CursedPearl : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override async Task AfterObtained()
    {
        await CardPileCmd.AddCursesToDeck(new[] { ModelDb.Card<Greed>() }, Owner);
        await PlayerCmd.GainGold(333m, Owner);
    }
}
