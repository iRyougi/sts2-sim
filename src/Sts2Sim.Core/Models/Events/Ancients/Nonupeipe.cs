using Sts2Sim.Core.Events;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Events;

public sealed class Nonupeipe : AncientEventModel
{
    private static readonly Type[] BaseCandidates =
    [
        typeof(BlessedAntler), typeof(BrilliantScarf), typeof(DelicateFrond), typeof(DiamondDiadem),
        typeof(FurCoat), typeof(Glitter), typeof(JewelryBox), typeof(LoomingFruit), typeof(SignetRing),
    ];

    public override IReadOnlyList<RelicModel> AllPossibleOptions => BaseCandidates.Append(typeof(BeautifulBracelet))
        .Select(type => (RelicModel)ModelDb.Get(type).MutableClone()).ToArray();

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        List<Type> candidates = Candidates();
        candidates.UnstableShuffle(Rng);
        return candidates.Take(3).Select(type => RelicOption(type, type.Name)).ToArray();
    }

    private List<Type> Candidates()
    {
        var candidates = BaseCandidates.ToList();
        if (Owner.Deck.Cards.Count(card => ((EnchantmentModel)ModelDb.Get(typeof(Swift))).CanEnchant(card)) >= 4)
            candidates.Add(typeof(BeautifulBracelet));
        return candidates;
    }
}
