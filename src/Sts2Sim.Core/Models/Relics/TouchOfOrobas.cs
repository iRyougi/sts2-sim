using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class TouchOfOrobas : RelicModel
{
    private ModelId? _starterRelic;
    private ModelId? _upgradedRelic;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public ModelId? StarterRelic
    {
        get => _starterRelic;
        set
        {
            AssertMutable();
            if (_starterRelic is not null)
                throw new InvalidOperationException("Recursive Core setup called twice!");
            _starterRelic = value;
        }
    }

    public ModelId? UpgradedRelic
    {
        get => _upgradedRelic;
        set
        {
            AssertMutable();
            if (_upgradedRelic is not null)
                throw new InvalidOperationException("Recursive Core setup called twice!");
            _upgradedRelic = value;
        }
    }

    public RelicModel GetUpgradedStarterRelic(RelicModel starterRelic)
    {
        ModelId id = starterRelic.Id;
        if (id == ModelDb.Relic<BurningBlood>().Id) return ModelDb.Relic<BlackBlood>();
        if (id == ModelDb.Relic<RingOfTheSnake>().Id) return ModelDb.Relic<RingOfTheDrake>();
        if (id == ModelDb.Relic<DivineRight>().Id) return ModelDb.Relic<DivineDestiny>();
        if (id == ModelDb.Relic<BoundPhylactery>().Id) return ModelDb.Relic<PhylacteryUnbound>();
        if (id == ModelDb.Relic<CrackedCore>().Id) return ModelDb.Relic<InfusedCore>();
        return ModelDb.Relic<Circlet>();
    }

    public bool SetupForPlayer(Player player)
    {
        AssertMutable();
        RelicModel? starterRelic = player.Relics.FirstOrDefault(relic => relic.Rarity == RelicRarity.Starter);
        if (starterRelic is null) return false;
        StarterRelic = starterRelic.Id;
        UpgradedRelic = GetUpgradedStarterRelic(starterRelic).Id;
        return true;
    }

    public override async Task AfterObtained()
    {
        ModelId starterId = StarterRelic ?? Owner.Relics.First(relic => relic.Rarity == RelicRarity.Starter).Id;
        RelicModel starter = Owner.Relics.First(relic => relic.Id == starterId);
        ModelId upgradedId = UpgradedRelic ?? GetUpgradedStarterRelic(starter).Id;
        RelicModel upgraded = (RelicModel)ModelDb.GetById<RelicModel>(upgradedId).MutableClone();
        await RelicCmd.Replace(starter, upgraded);
    }
}
