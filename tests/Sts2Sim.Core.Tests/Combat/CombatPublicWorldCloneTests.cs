namespace Sts2Sim.Core.Tests.Combat;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

/// <summary>
/// 公开世界克隆（<see cref="CombatState.CloneForPublicWorld"/>）：世界只取决于公开状态与世界种子，
/// 不取决于真实抽牌顺序、任何真实 RNG 流或遗物袋顺序。
/// </summary>
[Collection("ModelDb")]
public sealed class CombatPublicWorldCloneTests : IDisposable
{
    private static readonly ulong[] Seeds = { 11UL, 12UL, 13UL, 14UL, 15UL };

    public CombatPublicWorldCloneTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(typeof(WanderingGrunt), 0)] // 开局第 1 回合
    [InlineData(typeof(Fogmog), 2)]         // 打过两个整回合，第 3 回合已出牌，弃牌堆非空
    public async Task PublicWorld_DependsOnPublicStateAndSeedOnly(Type monsterType, int turnsPlayed)
    {
        string fixture = $"{monsterType.Name}/turns={turnsPlayed}";
        CombatState live = await EnterFight(monsterType, turnsPlayed);
        Player player = live.Players[0];
        CardPile draw = player.PlayerCombatState!.DrawPile;
        string[] drawIds = draw.Cards.Select(card => card.Id.Entry).ToArray();
        Assert.True(live.Engine!.IsInProgress, $"{fixture}: fixture fight ended early");
        Assert.True(drawIds.Distinct().Count() > 1 && drawIds.Distinct().Count() < drawIds.Length,
            $"{fixture}: draw pile needs distinct cards and a repeated card, got [{string.Join(',', drawIds)}]");
        Assert.True(turnsPlayed == 0 || player.PlayerCombatState.DiscardPile.Cards.Count > 0,
            $"{fixture}: mid-fight fixture needs a non-empty discard pile");

        CombatStateDescriptionDigest liveBefore = Exact(live);
        string publicBefore = PublicView(live);
        string orderBefore = DrawOrder(live);
        string bagBefore = RelicBag(live);
        CombatState[] worlds = Seeds.Select(live.CloneForPublicWorld).ToArray();
        CombatStateDescriptionDigest[] digests = worlds.Select(Exact).ToArray();
        string[] bags = worlds.Select(RelicBag).ToArray();

        // 1. 同种子可复现；不同种子给出不同世界，抽牌顺序与遗物袋顺序随种子变化。
        for (int i = 0; i < Seeds.Length; i++)
        {
            CombatState again = live.CloneForPublicWorld(Seeds[i]);
            Assert.True(digests[i] == Exact(again), $"{fixture} seed={Seeds[i]}: same seed, different world");
            Assert.True(bags[i] == RelicBag(again), $"{fixture} seed={Seeds[i]}: same seed, different relic bag");
        }
        Assert.True(digests.Distinct().Count() == Seeds.Length, $"{fixture}: two world seeds gave the same world");
        Assert.True(worlds.Select(world => string.Join(',', world.Players[0].PlayerCombatState!.DrawPile.Cards
                .Select(card => card.Id.Entry))).Distinct().Count() > 1,
            $"{fixture}: no seed changed the visible draw order");
        Assert.True(bags.Distinct().Count() > 1, $"{fixture}: no seed changed the relic bag order");

        // 2. 公开状态原样保留；RNG 身份不是真实的。
        for (int i = 0; i < Seeds.Length; i++)
        {
            Assert.True(publicBefore == PublicView(worlds[i]),
                $"{fixture} seed={Seeds[i]}: public state differs\nlive : {publicBefore}\nworld: {PublicView(worlds[i])}");
            Assert.True(live.RunState.Rng.Seed != worlds[i].RunState.Rng.Seed
                && player.PlayerRng.Seed != worlds[i].Players[0].PlayerRng.Seed
                && worlds[i].Players[0].PlayerRng.Seed != worlds[i].RunState.Rng.Seed,
                $"{fixture} seed={Seeds[i]}: world reuses a real RNG seed");
        }

        // 4. 在世界里打 3 个动作（最后一个是结束回合），真实战斗不变。
        CombatState played = live.CloneForPublicWorld(Seeds[0]);
        for (int action = 0; action < 3; action++)
        {
            if (action == 2 || !await PlayOneCard(played))
            {
                await played.Engine!.EndPlayerTurnAsync();
            }
        }
        Assert.True(digests[0] != Exact(played), $"{fixture} seed={Seeds[0]}: the world did not advance");
        Assert.True(liveBefore == Exact(live) && bagBefore == RelicBag(live),
            $"{fixture} seed={Seeds[0]}: playing in a world changed the live fight");

        // 3. 孪生状态：公开状态相同，隐藏状态不同（抽牌顺序反转、每条 RNG 流多走一步、遗物袋换序）。
        foreach (CardModel card in draw.Cards.Reverse().ToList())
        {
            draw.MoveToBottomInternal(card);
        }
        foreach (RunRngType stream in Enum.GetValues<RunRngType>())
        {
            live.RunState.Rng.GetRng(stream).NextInt(100);
        }
        foreach (PlayerRngType stream in Enum.GetValues<PlayerRngType>())
        {
            player.PlayerRng.GetRng(stream).NextInt(100);
        }
        foreach (Creature creature in live.Allies.Concat(live.Enemies))
        {
            creature.Monster?.Rng.NextInt(100);
        }
        typeof(Player).GetProperty(nameof(Player.RelicGrabBag))!
            .SetValue(player, player.RelicGrabBag.CloneReshuffled(new Rng(977)));

        Assert.True(publicBefore == PublicView(live), $"{fixture}: twin changed the public state");
        Assert.True(liveBefore != Exact(live) && orderBefore != DrawOrder(live) && bagBefore != RelicBag(live),
            $"{fixture}: twin does not differ in hidden state");
        for (int i = 0; i < Seeds.Length; i++)
        {
            CombatState fromTwin = live.CloneForPublicWorld(Seeds[i]);
            Assert.True(digests[i] == Exact(fromTwin),
                $"{fixture} seed={Seeds[i]}: world depends on hidden state (draw order or RNG streams)\n" +
                $"from live: {DrawOrder(worlds[i])}\nfrom twin: {DrawOrder(fromTwin)}");
            Assert.True(bags[i] == RelicBag(fromTwin),
                $"{fixture} seed={Seeds[i]}: world relic bag depends on the real bag order");
        }
    }

    private static async Task<CombatState> EnterFight(Type monsterType, int turnsPlayed)
    {
        var run = new RunState($"public-world-{monsterType.Name}", new Overgrowth(), 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Get(monsterType).MutableClone());
        run.PushRoom(room);
        await room.Enter(run);
        CombatState state = room.Engine.State;
        for (int turn = 0; turn < turnsPlayed; turn++)
        {
            while (await PlayOneCard(state)) { }
            await room.Engine.EndPlayerTurnAsync();
        }
        for (int card = 0; card < 2 && turnsPlayed > 0; card++)
        {
            await PlayOneCard(state);
        }
        return state;
    }

    // Survivor 需要选牌；夹具只打不需要选择的牌。
    private static async Task<bool> PlayOneCard(CombatState state)
    {
        Player player = state.Players[0];
        CardModel? card = player.PlayerCombatState!.Hand.Cards
            .FirstOrDefault(candidate => candidate is not Survivor && candidate.CanPlay(out _));
        if (card is null || !state.Engine!.IsInProgress)
        {
            return false;
        }
        Creature? target = card.TargetType == TargetType.AnyEnemy ? state.HittableEnemies.FirstOrDefault() : null;
        await state.Engine.PlayCardAsync(player, card, target);
        return true;
    }

    private static CombatStateDescriptionDigest Exact(CombatState state)
    {
        var builder = new CombatStateDescriptionBuilder();
        CombatStateDescription.AppendExactState(ref builder, state);
        return builder.Build();
    }

    private static string CardFace(CardModel card, CombatState state)
    {
        var builder = new CombatStateDescriptionBuilder();
        CombatStateDescription.AppendCardState(ref builder, card, state);
        CombatStateDescriptionDigest digest = builder.Build();
        return $"{card.Id.Entry}:{digest.First:X16}{digest.Second:X16}";
    }

    // 抽牌顺序：牌名 + 对应的牌组位置（同名牌之间也能分出先后）。
    private static string DrawOrder(CombatState state)
    {
        Player player = state.Players[0];
        List<CardModel> deck = player.Deck.Cards.ToList();
        return string.Join(',', player.PlayerCombatState!.DrawPile.Cards.Select(card =>
            $"{card.Id.Entry}@{(card.DeckVersion is { } deckVersion ? deck.IndexOf(deckVersion) : -1)}"));
    }

    private static string RelicBag(CombatState state) => string.Join(';',
        state.Players[0].RelicGrabBag.ExportForTransplant().Buckets
            .OrderBy(bucket => bucket.Key, StringComparer.Ordinal)
            .Select(bucket => $"{bucket.Key}:{string.Join(',', bucket.Value)}"));

    // 玩家看得到的战斗状态：手牌、弃牌、消耗、出牌区按顺序，抽牌堆按牌面多重集，双方血量格挡能力与意图。
    private static string PublicView(CombatState state)
    {
        Player player = state.Players[0];
        PlayerCombatState combat = player.PlayerCombatState!;
        string Pile(CardPile pile, bool ordered)
        {
            IEnumerable<string> faces = pile.Cards.Select(card => CardFace(card, state));
            return $"{pile.Type}[{string.Join(',', ordered ? faces : faces.Order(StringComparer.Ordinal))}]";
        }
        string Body(Creature creature) =>
            $"{creature.CombatId}:{creature.Monster?.Id.Entry ?? "player"}:{creature.CurrentHp}/{creature.MaxHp}:" +
            $"block={creature.Block}:" +
            $"powers=[{string.Join(',', creature.Powers.Select(power => $"{power.Id.Entry}={power.Amount}"))}]:" +
            $"intent={creature.Monster?.NextMove?.StateId}";
        return string.Join('|', new[]
        {
            $"side={state.CurrentSide}", $"round={state.RoundNumber}", $"energy={combat.Energy}",
            Pile(combat.Hand, true), Pile(combat.DiscardPile, true), Pile(combat.ExhaustPile, true),
            Pile(combat.PlayPile, true), Pile(combat.DrawPile, false),
            $"potions=[{string.Join(',', player.PotionSlots.Select(potion => potion?.Id.Entry ?? "-"))}]",
            $"relics=[{string.Join(',', player.Relics.Select(relic => relic.Id.Entry))}]",
            $"allies=[{string.Join(';', state.Allies.Select(Body))}]",
            $"enemies=[{string.Join(';', state.Enemies.Select(Body))}]",
        });
    }
}
