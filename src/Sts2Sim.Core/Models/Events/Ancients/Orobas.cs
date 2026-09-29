using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events
{
    using Sts2Sim.Core.Models.Relics;

    /// <summary>
    /// Orobas Ancient reward selection.
    /// SeaGlass character binding and Touch payload limitations are recorded per relic in #212-#213;
    /// event-pool eligibility and locked third-option semantics remain faithful.
    /// </summary>
    public sealed class Orobas : AncientEventModel
    {
        private static readonly Type[] Pool1 = { typeof(ElectricShrymp), typeof(GlassEye) };
        private static readonly Type[] Pool2 =
        {
            typeof(AlchemicalCoffer), typeof(Driftwood), typeof(RadiantPearl), typeof(SandCastle),
        };

        public override IReadOnlyList<RelicModel> AllPossibleOptions =>
            Pool1.Concat(Pool2)
                .Concat(new[] { typeof(TouchOfOrobas), typeof(ArchaicTooth) })
                .Select(CloneRelic)
                .Concat(ModelDb.All<CharacterModel>().Where(character => character.IsPlayable)
                    .Select(_ => CloneRelic(typeof(SeaGlass))))
                .Append(CloneRelic(typeof(PrismaticGem)))
                .ToArray();

        protected override IReadOnlyList<EventOption> GenerateInitialOptions()
        {
            List<CharacterModel> otherCharacters = ModelDb.All<CharacterModel>()
                .Where(character => character.IsPlayable && character.GetType() != Owner.Character.GetType())
                .ToList();
            _ = Rng.NextItem(otherCharacters) ?? Owner.Character;

            Type thirdPool1Candidate = Rng.NextFloat() < (1f / 3f)
                ? typeof(PrismaticGem)
                : typeof(SeaGlass);
            Type[] firstCandidates = Pool1.Append(thirdPool1Candidate).ToArray();
            Type[] pool3 = BuildPool3();

            return new[]
            {
                Option(Rng.NextItem(firstCandidates)!),
                Option(Rng.NextItem(Pool2)!),
                pool3.Length == 0
                    ? new EventOption("OPTION_POOL_3_LOCKED", null)
                    : Option(Rng.NextItem(pool3)!),
            };
        }

        private Type[] BuildPool3()
        {
            var result = new List<Type>();
            if (Owner.Relics.Any(relic => relic.Rarity == RelicRarity.Starter))
            {
                result.Add(typeof(TouchOfOrobas));
            }
            if (Owner.Deck.Cards.Any(ArchaicTooth.IsTranscendenceStarter))
            {
                result.Add(typeof(ArchaicTooth));
            }
            return result.ToArray();
        }

        private EventOption Option(Type relicType) => RelicOption(relicType, relicType.Name);

        private static RelicModel CloneRelic(Type type) => (RelicModel)ModelDb.Get(type).MutableClone();
    }
}
