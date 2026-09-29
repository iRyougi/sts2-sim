using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class StoneHumidifierTests : IDisposable
{
    public StoneHumidifierTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task AfterRestSiteHealHook_IncreasesOnlyOwnerMaxHp()
    {
        RunState runState = CreateRun("stone-humidifier-hook", out Player owner, out Player foreign);
        await RelicCmd.Obtain(ModelDb.Relic<StoneHumidifier>(), owner);
        int ownerMaxBefore = owner.Creature.MaxHp;
        int foreignMaxBefore = foreign.Creature.MaxHp;

        await Hook.AfterRestSiteHeal(runState, foreign);
        Assert.Equal(ownerMaxBefore, owner.Creature.MaxHp);
        Assert.Equal(foreignMaxBefore, foreign.Creature.MaxHp);

        await Hook.AfterRestSiteHeal(runState, owner);
        Assert.Equal(ownerMaxBefore + 5, owner.Creature.MaxHp);
        Assert.Equal(foreignMaxBefore, foreign.Creature.MaxHp);
    }

    [Fact]
    public async Task ResolveHeal_DispatchesExactlyOnceAfterActualHealCompletes()
    {
        RunState runState = CreateRun("stone-humidifier-resolve", out Player owner, out _);
        await RelicCmd.Obtain(ModelDb.Relic<StoneHumidifier>(), owner);
        int maxBefore = owner.Creature.MaxHp;
        owner.Creature.LoseHpInternal(maxBefore - 1, default(ValueProp));
        int hpBefore = owner.Creature.CurrentHp;

        await new RestSiteRoom().ResolveAsync(owner, new RestSiteDecision.Heal());

        int expectedRestHeal = (int)(maxBefore * 0.3m);
        Assert.Equal(maxBefore + 5, owner.Creature.MaxHp);
        Assert.Equal(hpBefore + expectedRestHeal + 5, owner.Creature.CurrentHp);
    }

    [Fact]
    public async Task SmithAndHatch_DoNotDispatchRestSiteHealHook()
    {
        RunState smithRun = CreateRun("stone-humidifier-smith", out Player smithOwner, out _);
        await RelicCmd.Obtain(ModelDb.Relic<StoneHumidifier>(), smithOwner);
        int smithMaxBefore = smithOwner.Creature.MaxHp;
        CardModel smithCard = smithOwner.Deck.Cards.First(card => card.IsUpgradable);

        await new RestSiteRoom().ResolveAsync(smithOwner, new RestSiteDecision.Smith(smithCard));
        Assert.Equal(smithMaxBefore, smithOwner.Creature.MaxHp);

        RunState hatchRun = CreateRun("stone-humidifier-hatch", out Player hatchOwner, out _);
        await RelicCmd.Obtain(ModelDb.Relic<StoneHumidifier>(), hatchOwner);
        CardModel egg = (CardModel)ModelDb.Card<ByrdonisEgg>().MutableClone();
        egg.AssignOwner(hatchOwner);
        await CardPileCmd.AddToDeck(egg);
        int hatchMaxBefore = hatchOwner.Creature.MaxHp;

        await new RestSiteRoom().ResolveAsync(hatchOwner, new RestSiteDecision.Hatch());
        Assert.Equal(hatchMaxBefore, hatchOwner.Creature.MaxHp);
    }

    [Fact]
    public void Metadata_MatchesAncientOngoingRelic()
    {
        StoneHumidifier relic = ModelDb.Relic<StoneHumidifier>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.IsUsedUp);
    }

    private static RunState CreateRun(string seed, out Player owner, out Player foreign)
    {
        var runState = new RunState(seed, new Overgrowth());
        owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        foreign = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(foreign);
        return runState;
    }
}