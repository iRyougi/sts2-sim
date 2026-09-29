using Sts2Sim.Core.Events;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Events;

public sealed class Tanx : AncientEventModel
{
    private static readonly Type[] BaseCandidates =
    [
        typeof(Claws), typeof(Crossbow), typeof(IronClub), typeof(MeatCleaver), typeof(Sai),
        typeof(SpikedGauntlets), typeof(TanxsWhistle), typeof(ThrowingAxe), typeof(WarHammer),
    ];

    public override IReadOnlyList<RelicModel> AllPossibleOptions => BaseCandidates.Append(typeof(TriBoomerang))
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
        if (Owner.Deck.Cards.Count(card => ((EnchantmentModel)ModelDb.Get(typeof(Instinct))).CanEnchant(card)) >= 3)
            candidates.Add(typeof(TriBoomerang));
        return candidates;
    }
}
