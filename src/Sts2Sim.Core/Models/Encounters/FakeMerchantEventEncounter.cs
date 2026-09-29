using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models.Monsters;

namespace Sts2Sim.Core.Models.Encounters;

public static class FakeMerchantEventEncounter
{
    public const int GoldReward = 300;
    public static EncounterDefinition Definition => new(
        () => new[] { ((MonsterModel)ModelDb.Monster<FakeMerchantMonster>().MutableClone(), (string?)"merchant") },
        tags: null, isWeak: false, name: "FakeMerchantEventEncounter");
}
