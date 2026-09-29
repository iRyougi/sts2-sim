using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Events
{
    using Sts2Sim.Core.Models.Relics;

    /// <summary>
    /// Pael Ancient selection. Goopy eligibility is delegated to the authoritative Goopy.CanEnchant gate;
    /// the authoritative 3/5-card gates,
    /// duplicated Wing/Claw/Tooth weighting, Growth entry, pet veto, and three RNG draws are preserved.
    /// </summary>
    public sealed class Pael : AncientEventModel
    {
        private static readonly Type[] Pool1 = { typeof(PaelsFlesh), typeof(PaelsHorn), typeof(PaelsTears) };
        private static readonly Type[] Pool3 = { typeof(PaelsEye), typeof(PaelsBlood) };
        private static readonly Type[] AllTypes =
        {
            typeof(PaelsFlesh), typeof(PaelsHorn), typeof(PaelsTears), typeof(PaelsWing),
            typeof(PaelsEye), typeof(PaelsBlood), typeof(PaelsClaw), typeof(PaelsTooth),
            typeof(PaelsLegion), typeof(PaelsGrowth),
        };

        public override IReadOnlyList<RelicModel> AllPossibleOptions =>
            AllTypes.Select(type => (RelicModel)ModelDb.Get(type).MutableClone()).ToArray();

        protected override IReadOnlyList<EventOption> GenerateInitialOptions()
        {
            Type first = Rng.NextItem(Pool1)!;
            var secondCandidates = new List<Type> { typeof(PaelsWing) };
            if (Owner.Deck.Cards.Count(IsGoopyEligible) >= 3)
            {
                secondCandidates.Add(typeof(PaelsClaw));
            }
            if (Owner.Deck.Cards.Count(card => card.IsRemovable) >= 5)
            {
                secondCandidates.Add(typeof(PaelsTooth));
            }
            secondCandidates.AddRange(secondCandidates.ToArray());
            secondCandidates.Add(typeof(PaelsGrowth));
            Type second = Rng.NextItem(secondCandidates)!;

            var thirdCandidates = Pool3.ToList();
            if (!Owner.HasEventPet())
            {
                thirdCandidates.Add(typeof(PaelsLegion));
            }
            Type third = Rng.NextItem(thirdCandidates)!;
            return new[] { Option(first), Option(second), Option(third) };
        }

        private static bool IsGoopyEligible(CardModel card) =>
            ModelDb.GetById<Goopy>(ModelDb.GetId<Goopy>()).CanEnchant(card);

        private EventOption Option(Type type) => RelicOption(type, type.Name);
    }
}
