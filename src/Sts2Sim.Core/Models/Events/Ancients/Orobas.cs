using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Events
{
    using Sts2Sim.Core.Models.Relics;

    /// <summary>
    /// Orobas Ancient reward selection.
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
                    .Select(character => (RelicModel)ForCharacter(character)))
                .Append(CloneRelic(typeof(PrismaticGem)))
                .ToArray();

        protected override IReadOnlyList<EventOption> GenerateInitialOptions()
        {
            List<CharacterModel> otherCharacters = ModelDb.All<CharacterModel>()
                .Where(character => character.IsPlayable && character.GetType() != Owner.Character.GetType())
                .ToList();
            CharacterModel seaGlassCharacter = Rng.NextItem(otherCharacters) ?? Owner.Character;

            Type thirdPool1Candidate = Rng.NextFloat() < (1f / 3f)
                ? typeof(PrismaticGem)
                : typeof(SeaGlass);
            Type[] firstCandidates = Pool1.Append(thirdPool1Candidate).ToArray();
            EventOption[] pool3 = BuildPool3();

            return new[]
            {
                Option(Rng.NextItem(firstCandidates)!, seaGlassCharacter),
                Option(Rng.NextItem(Pool2)!),
                pool3.Length == 0
                    ? new EventOption("OPTION_POOL_3_LOCKED", null)
                    : Rng.NextItem(pool3)!,
            };
        }

        private EventOption[] BuildPool3()
        {
            var result = new List<EventOption>();
            var touch = (TouchOfOrobas)ModelDb.Relic<TouchOfOrobas>().MutableClone();
            if (touch.SetupForPlayer(Owner))
            {
                result.Add(RelicOption(touch, nameof(TouchOfOrobas)));
            }
            if (Owner.Deck.Cards.Any(ArchaicTooth.IsTranscendenceStarter))
            {
                result.Add(Option(typeof(ArchaicTooth)));
            }
            return result.ToArray();
        }

        private EventOption Option(Type relicType, CharacterModel? seaGlassCharacter = null)
        {
            if (relicType != typeof(SeaGlass)) return RelicOption(relicType, relicType.Name);
            SeaGlass relic = ForCharacter(seaGlassCharacter ?? Owner.Character);
            return new EventOption(nameof(SeaGlass), async () =>
            {
                await RelicCmd.Obtain(relic, Owner);
                Finish();
            });
        }

        private static SeaGlass ForCharacter(CharacterModel character)
        {
            var relic = (SeaGlass)ModelDb.Relic<SeaGlass>().MutableClone();
            relic.CharacterId = character.Id;
            return relic;
        }

        private static RelicModel CloneRelic(Type type) => (RelicModel)ModelDb.Get(type).MutableClone();
    }
}
