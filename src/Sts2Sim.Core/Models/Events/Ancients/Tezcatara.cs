using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;

namespace Sts2Sim.Core.Models.Events
{
    using Sts2Sim.Core.Models.Relics;

    /// <summary>
    /// Tezcatara Ancient selection. Basic Strike gating and the three authoritative random pools are exact.
    /// Deviation #204: Ancient dialogue/history and Defect's dialogue blacklist are absent from the headless base.
    /// </summary>
    public sealed class Tezcatara : AncientEventModel
    {
        private static readonly Type[] Pool1 = { typeof(VeryHotCocoa), typeof(YummyCookie) };
        private static readonly Type[] Pool2 = { typeof(BiiigHug), typeof(Storybook), typeof(ToastyMittens) };
        private static readonly Type[] Pool3 =
        {
            typeof(GoldenCompass), typeof(PumpkinCandle), typeof(ToyBox), typeof(SealOfGold),
        };
        private static readonly Type[] AllTypes = Pool1.Concat(Pool2).Concat(Pool3)
            .Append(typeof(NutritiousSoup)).ToArray();

        public override IReadOnlyList<RelicModel> AllPossibleOptions =>
            AllTypes.Select(type => (RelicModel)ModelDb.Get(type).MutableClone()).ToArray();

        protected override IReadOnlyList<EventOption> GenerateInitialOptions()
        {
            var firstCandidates = Pool1.ToList();
            if (Owner.Deck.Cards.Any(card => card.Rarity == CardRarity.Basic && card.Tags.Contains(CardTag.Strike)))
            {
                firstCandidates.Add(typeof(NutritiousSoup));
            }
            return new[]
            {
                Option(Rng.NextItem(firstCandidates)!),
                Option(Rng.NextItem(Pool2)!),
                Option(Rng.NextItem(Pool3)!),
            };
        }

        private EventOption Option(Type type) => RelicOption(type, type.Name);
    }
}
