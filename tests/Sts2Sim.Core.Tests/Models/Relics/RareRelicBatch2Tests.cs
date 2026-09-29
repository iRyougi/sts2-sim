using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

file sealed class Task8CostlySkillCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
}

file sealed class Task8XEnergySkillCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override bool IsXEnergyCost => true;
}

file sealed class Task8PowerCard : CardModel
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class Task8StarSkillCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override int CanonicalStarCost => 1;
}

file sealed class Task8ZeroSkillCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
}

[Collection("ModelDb")]
public sealed class RareRelicBatch2Tests : IDisposable
{
    public RareRelicBatch2Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(Task8CostlySkillCard))
                .Append(typeof(Task8XEnergySkillCard))
                .Append(typeof(Task8PowerCard))
                .Append(typeof(Task8StarSkillCard))
                .Append(typeof(Task8ZeroSkillCard)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Task8Relics_AreAllRegisteredAsRare()
    {
        RelicModel[] relics =
        {
            ModelDb.Relic<LunarPastry>(),
            ModelDb.Relic<Mango>(),
            ModelDb.Relic<MeatOnTheBone>(),
            ModelDb.Relic<MiniRegent>(),
            ModelDb.Relic<MummifiedHand>(),
            ModelDb.Relic<OldCoin>(),
            ModelDb.Relic<OrangeDough>(),
            ModelDb.Relic<Pocketwatch>(),
        };

        Assert.Equal(8, relics.Length);
        Assert.All(relics, relic => Assert.Equal(RelicRarity.Rare, relic.Rarity));
    }

    [Fact]
    public async Task LunarPastry_GrantsOneStarOnlyAtOwnedPlayerTurnEnd()
    {
        (RunState runState, Player player) = CreateRun("lunar-pastry");
        await Obtain<LunarPastry>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        int starsBefore = player.PlayerCombatState!.Stars;

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(starsBefore + 1, player.PlayerCombatState!.Stars);
    }

    [Fact]
    public async Task Mango_IncreasesMaximumAndCurrentHpByFourteen()
    {
        (_, Player player) = CreateRun("mango");
        int maxHpBefore = player.Creature.MaxHp;
        int currentHpBefore = player.Creature.CurrentHp;

        await Obtain<Mango>(player);

        Mango mango = Assert.Single(player.Relics.OfType<Mango>());
        Assert.True(mango.HasUponPickupEffect);
        Assert.Equal(maxHpBefore + 14, player.Creature.MaxHp);
        Assert.Equal(currentHpBefore + 14, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task MeatOnTheBone_HealsTwelveAtOrBelowHalfHpOnly()
    {
        (RunState lowRun, Player lowPlayer) = CreateRun("meat-low");
        await Obtain<MeatOnTheBone>(lowPlayer);
        CombatRoom lowRoom = CreateCombatRoom();
        await lowRoom.Enter(lowRun);
        int halfHp = lowPlayer.Creature.MaxHp / 2;
        lowPlayer.Creature.LoseHpInternal(
            lowPlayer.Creature.CurrentHp - halfHp,
            ValueProp.Unpowered);

        await Hook.AfterCombatVictory(lowRoom.Engine.State);

        Assert.Equal(halfHp + 12, lowPlayer.Creature.CurrentHp);

        (RunState highRun, Player highPlayer) = CreateRun("meat-high");
        await Obtain<MeatOnTheBone>(highPlayer);
        CombatRoom highRoom = CreateCombatRoom();
        await highRoom.Enter(highRun);
        int aboveHalfHp = highPlayer.Creature.MaxHp / 2 + 1;
        highPlayer.Creature.LoseHpInternal(
            highPlayer.Creature.CurrentHp - aboveHalfHp,
            ValueProp.Unpowered);

        await Hook.AfterCombatVictory(highRoom.Engine.State);

        Assert.Equal(aboveHalfHp, highPlayer.Creature.CurrentHp);
    }

    [Fact]
    public async Task MiniRegent_GrantsStrengthOnlyOncePerOwnedTurn()
    {
        (RunState runState, Player player) = CreateRun("mini-regent");
        await Obtain<MiniRegent>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        await PlayerCmd.GainStars(3m, player);

        await room.Engine.PlayCardAsync(player, AddToHand<Task8StarSkillCard>(player), null);
        await room.Engine.PlayCardAsync(player, AddToHand<Task8StarSkillCard>(player), null);

        Assert.Equal(1, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);

        await room.Engine.EndPlayerTurnAsync();
        await PlayerCmd.GainStars(1m, player);
        await room.Engine.PlayCardAsync(player, AddToHand<Task8StarSkillCard>(player), null);

        Assert.Equal(2, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
    }

    [Fact]
    public async Task MummifiedHand_MakesExactlyOneRandomHandCardFree()
    {
        (RunState runState, Player player) = CreateRun("mummified-one");
        await Obtain<MummifiedHand>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(player);
        Task8CostlySkillCard first = AddToHand<Task8CostlySkillCard>(player);
        Task8CostlySkillCard second = AddToHand<Task8CostlySkillCard>(player);

        await room.Engine.PlayCardAsync(player, AddToHand<Task8PowerCard>(player), player.Creature);

        Assert.Equal(1, new[] { first, second }.Count(card => card.EnergyCost == 0));
    }

    [Fact]
    public async Task MummifiedHand_PrefersACardThatStillCostsEnergyOrStarsOverAnAlreadyFreeOne()
    {
        (RunState runState, Player player) = CreateRun("mummified-priority");
        await Obtain<MummifiedHand>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(player);
        AddToHand<Task8ZeroSkillCard>(player);
        Task8CostlySkillCard costly = AddToHand<Task8CostlySkillCard>(player);

        await room.Engine.PlayCardAsync(player, AddToHand<Task8PowerCard>(player), player.Creature);

        // 真实源码优先选"仍需要花费能量/星愿"的候选（偏离 #137）；候选池里只有 costly 符合条件，
        // 所以结果是确定的——如果实现退化成对整手牌均匀随机选，这条断言会在 costly 未被选中的那次运行里失败。
        Assert.Equal(0, costly.EnergyCost);
    }

    [Fact]
    public async Task MummifiedHand_FreePlaySpendsNoEnergyAndClearsAfterMovedPileAtTurnEnd()
    {
        (RunState runState, Player player) = CreateRun("mummified-cleanup");
        await Obtain<MummifiedHand>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(player);
        Task8CostlySkillCard candidate = AddToHand<Task8CostlySkillCard>(player);

        await room.Engine.PlayCardAsync(player, AddToHand<Task8PowerCard>(player), player.Creature);
        int energyBefore = player.PlayerCombatState!.Energy;
        await room.Engine.PlayCardAsync(player, candidate, player.Creature);

        Assert.Equal(energyBefore, player.PlayerCombatState.Energy);
        Assert.Equal(PileType.Discard, candidate.Pile!.Type);
        Assert.Equal(0, candidate.EnergyCost);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(2, candidate.EnergyCost);
    }

    [Fact]
    public async Task MummifiedHand_FreeXCardStillSpendsCurrentEnergy()
    {
        (RunState runState, Player player) = CreateRun("mummified-x");
        await Obtain<MummifiedHand>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        ClearHand(player);
        Task8XEnergySkillCard candidate = AddToHand<Task8XEnergySkillCard>(player);

        await room.Engine.PlayCardAsync(player, AddToHand<Task8PowerCard>(player), player.Creature);
        int energyBefore = player.PlayerCombatState!.Energy;
        await room.Engine.PlayCardAsync(player, candidate, player.Creature);

        Assert.Equal(0, candidate.EnergyCost);
        Assert.Equal(0, player.PlayerCombatState.Energy);
        Assert.True(energyBefore > 0);
    }

    [Fact]
    public async Task OldCoin_GrantsThreeHundredGoldAndIsExcludedFromShops()
    {
        (_, Player player) = CreateRun("old-coin");
        int goldBefore = player.Gold;

        await Obtain<OldCoin>(player);

        OldCoin oldCoin = Assert.Single(player.Relics.OfType<OldCoin>());
        Assert.True(oldCoin.HasUponPickupEffect);
        Assert.False(oldCoin.IsAllowedInShops);
        Assert.Equal(goldBefore + 300, player.Gold);
    }

    [Fact]
    public async Task OrangeDough_GeneratesTwoDistinctColorlessCardsOnFirstTurnOnly()
    {
        (RunState runState, Player player) = CreateRun("orange-dough");
        await Obtain<OrangeDough>(player);
        CombatRoom room = CreateCombatRoom();

        await room.Enter(runState);

        CardModel[] generated =
            player.PlayerCombatState!.Hand.Cards.Where(card => card.IsColorless).ToArray();
        Assert.Equal(2, generated.Length);
        Assert.Equal(2, generated.Select(card => card.GetType()).Distinct().Count());
        // Native GetDistinctForCombat shuffles all 50 eligible colorless cards (49 draws).
        Assert.Equal(49, room.Engine.State.RunState.Rng.CombatCardGeneration.Counter);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(49, room.Engine.State.RunState.Rng.CombatCardGeneration.Counter);
    }

    [Fact]
    public async Task Pocketwatch_SkipsOpeningDrawThenUsesPrivatePriorTurnCountAtThreeCardBoundary()
    {
        (RunState runState, Player player) = CreateRun("pocketwatch");
        await Obtain<Pocketwatch>(player);
        CombatRoom room = CreateCombatRoom();

        await room.Enter(runState);

        Assert.Equal(5, player.PlayerCombatState!.Hand.Cards.Count);

        for (int i = 0; i < 3; i++)
        {
            await room.Engine.PlayCardAsync(player, AddToHand<Task8ZeroSkillCard>(player), null);
        }
        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(8, player.PlayerCombatState!.Hand.Cards.Count);

        for (int i = 0; i < 4; i++)
        {
            await room.Engine.PlayCardAsync(player, AddToHand<Task8ZeroSkillCard>(player), null);
        }
        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(5, player.PlayerCombatState!.Hand.Cards.Count);
    }

    private static Task Obtain<TRelic>(Player player)
        where TRelic : RelicModel =>
        RelicCmd.Obtain(ModelDb.Relic<TRelic>(), player);

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static void ClearHand(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
    }

    private static CombatRoom CreateCombatRoom() =>
        new(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
