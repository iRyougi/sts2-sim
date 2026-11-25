using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Characters;

[Collection("ModelDb")]
public sealed class FoolFoundationTests : IDisposable
{
    public FoolFoundationTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("three_hits_and_clone")]
    [InlineData("death_recovery")]
    [InlineData("escape_and_touchegg")]
    [InlineData("dead_target_retrieves")]
    [InlineData("non_fool_shoot_auto_slot")]
    public async Task ArrowSubsystem_RecoveryCloningAndNonFoolSlot_MatchSource(string scenario)
    {
        var run = new RunState($"fool-g1-arrow-{scenario}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);

        var room = scenario == "death_recovery"
            ? new CombatRoom(() => Enumerable.Range(0, 2)
                .Select(_ => (MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone())
                .ToArray())
            : new CombatRoom(() => scenario == "escape_and_touchegg"
                ? (MonsterModel)ModelDb.Monster<ToughEgg>().MutableClone()
                : (MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone());
        await room.Enter(run);
        CombatState state = room.Engine.State;
        Creature enemy = scenario == "death_recovery" ? state.Enemies[0] : Assert.Single(state.Enemies);
        foreach (Creature e in state.Enemies)
        {
            e.SetMaxHpInternal(200);
            e.SetCurrentHpInternal(200);
        }

        if (scenario == "non_fool_shoot_auto_slot")
        {
            Assert.Empty(player.PlayerCombatState!.Arrows);
            FoolishShot shot = AddToPile<FoolishShot>(player, PileType.Hand);
            Assert.Single(player.PlayerCombatState.Arrows);
            Assert.True(player.PlayerCombatState.Arrows[0].IsAvailable);

            await room.Engine.PlayCardAsync(player, shot, enemy);
            Assert.Equal(ArrowStatus.Away, player.PlayerCombatState.Arrows[0].Status);
            Assert.True(ArrowCmd.CreatureIsImpaledByPlayersArrow(enemy, player));

            CombatState clone = state.Clone();
            Player clonedPlayer = Assert.Single(clone.Players);
            Creature clonedEnemy = Assert.Single(clone.Enemies);
            Assert.Equal(ArrowStatus.Away, Assert.Single(clonedPlayer.PlayerCombatState!.Arrows).Status);
            ImpaledPower clonedImpaled = Assert.Single(clonedEnemy.Powers.OfType<ImpaledPower>());
            Assert.Same(clonedPlayer.PlayerCombatState.Arrows[0], Assert.Single(clonedImpaled.Arrows));
            return;
        }

        player.PlayerCombatState!.GainArrow();
        ArrowSlot slot = Assert.Single(player.PlayerCombatState.Arrows);
        if (scenario == "dead_target_retrieves")
        {
            // v0.114.0：发射后、穿刺前目标已死，ImpaleTarget 直接回收箭矢（DestroyAfterImpaled/Destroyed 已删）。
            Assert.Same(slot, await ArrowCmd.TryShootArrow(player, null));
            Assert.Equal(ArrowStatus.Away, slot.Status);
            await CreatureCmd.Kill(enemy);
            await ArrowCmd.ImpaleTarget(enemy, slot, null, null);
            Assert.Equal(ArrowStatus.Retrieved, slot.Status);
            Assert.Empty(enemy.Powers.OfType<ImpaledPower>());
            Assert.Equal(1, ArrowCmd.GetAvailableArrowCount(player));
            return;
        }

        FoolishShot starterShot = AddToPile<FoolishShot>(player, PileType.Hand);
        await room.Engine.PlayCardAsync(player, starterShot, enemy);

        ImpaledPower impaled = Assert.Single(enemy.Powers.OfType<ImpaledPower>());
        Assert.Equal(3, impaled.HitCounter);
        Assert.Equal(ArrowStatus.Away, slot.Status);

        switch (scenario)
        {
            case "three_hits_and_clone":
                player.PlayerCombatState!.Energy = 10;
                StrikeFool strike1 = AddToPile<StrikeFool>(player, PileType.Hand);
                Assert.NotNull(await room.Engine.PlayCardWithResultAsync(player, strike1, enemy));
                Assert.Equal(2, impaled.HitCounter);
                // v0.114.0：施加者判定看来源牌的持有者；无来源牌的攻击伤害不计数。
                await CreatureCmd.Damage(state, [enemy], 1m, ValueProp.Move, player.Creature, null, null);
                Assert.Equal(2, impaled.HitCounter);

                CombatState cloneMid = state.Clone();
                Creature clonedMidEnemy = Assert.Single(cloneMid.Enemies);
                ImpaledPower clonedMidImpaled = Assert.Single(clonedMidEnemy.Powers.OfType<ImpaledPower>());
                Assert.Equal(2, clonedMidImpaled.HitCounter);
                Assert.Same(
                    Assert.Single(Assert.Single(cloneMid.Players).PlayerCombatState!.Arrows),
                    Assert.Single(clonedMidImpaled.Arrows));

                StrikeFool strike2 = AddToPile<StrikeFool>(player, PileType.Hand);
                StrikeFool strike3 = AddToPile<StrikeFool>(player, PileType.Hand);
                Assert.NotNull(await room.Engine.PlayCardWithResultAsync(player, strike2, enemy));
                Assert.Equal(1, impaled.HitCounter);
                Assert.NotNull(await room.Engine.PlayCardWithResultAsync(player, strike3, enemy));

                Assert.Equal(ArrowStatus.Retrieved, slot.Status);
                Assert.Empty(enemy.Powers.OfType<ImpaledPower>());
                break;

            case "death_recovery":
                var dislodgeProbe = (DislodgeSourceProbePower)new DislodgeSourceProbePower().MutableClone();
                await PowerCmd.Apply(state, dislodgeProbe, player.Creature, 1m, player.Creature, null);
                await CreatureCmd.Kill(enemy, cardSource: starterShot);
                Assert.Equal(ArrowStatus.Retrieved, slot.Status);
                Assert.Empty(enemy.Powers.OfType<ImpaledPower>());
                Assert.Same(starterShot, dislodgeProbe.LastSource);
                break;

            case "escape_and_touchegg":
                // HatchMove clears ordinary powers while preserving MinionPower and ImpaledPower.
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(ArrowStatus.Away, slot.Status);
                Assert.Single(enemy.Powers.OfType<ImpaledPower>());

                await CreatureCmd.Escape(enemy);
                Assert.Equal(ArrowStatus.Retrieved, slot.Status);
                Assert.Empty(enemy.Powers.OfType<ImpaledPower>());
                break;
        }
    }

    [Theory]
    [InlineData("melody_nested_reentry")]
    [InlineData("adroit_stretch_and_sly_gate")]
    [InlineData("autoplay_mod_songbow_aim_theshot")]
    public async Task MelodyAndFoundationEntities_MatchSource(string scenario)
    {
        var run = new RunState($"fool-g1-melody-{scenario}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        if (scenario == "autoplay_mod_songbow_aim_theshot")
            await RelicCmd.Obtain(ModelDb.Relic<SongBow>(), player);

        var room = new CombatRoom(() => (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone());
        await room.Enter(run);
        CombatState state = room.Engine.State;
        Creature enemy = Assert.Single(state.Enemies);
        enemy.SetMaxHpInternal(200);
        enemy.SetCurrentHpInternal(200);
        PlayerCombatState pcs = player.PlayerCombatState!;

        switch (scenario)
        {
            case "melody_nested_reentry":
                var wahCard = (TestTagCard)new TestTagCard(CardTag.Wah).MutableClone();
                wahCard.AssignOwner(player);
                var wahOuter = new CardPlay
                {
                    Card = wahCard,
                    Player = player,
                    Target = null,
                    ResultPile = PileType.Discard,
                    Resources = new ResourceInfo { EnergyValue = 0, EnergySpent = 0 },
                    IsAutoPlay = false,
                    PlayIndex = 0,
                    PlayCount = 1,
                    PlayOrdinal = 1,
                };
                var wahNested = new CardPlay
                {
                    Card = wahCard,
                    Player = player,
                    Target = null,
                    ResultPile = PileType.Discard,
                    Resources = new ResourceInfo { EnergyValue = 0, EnergySpent = 0 },
                    IsAutoPlay = true,
                    PlayIndex = 0,
                    PlayCount = 1,
                    PlayOrdinal = 2,
                };
                pcs.RecordCardPlayStarted(wahOuter);
                // v0.114.0：PlayedThisTurn 读 CardPlaysStarted，出牌开始即算打出（结算完成前已为 true）。
                Assert.True(MelodyCmd.PlayedWahThisTurn(player));
                pcs.RecordCardPlayStarted(wahNested);
                pcs.RecordCardPlayed(wahCard, isAutoPlay: true);
                pcs.RecordCardPlayed(wahCard, isAutoPlay: false);

                Assert.True(MelodyCmd.IsFirstPlayThisTurn(wahOuter, CardTag.Wah));
                Assert.False(MelodyCmd.IsFirstPlayThisTurn(wahNested, CardTag.Wah));
                Assert.True(MelodyCmd.PlayedWahThisTurn(player));
                Assert.False(MelodyCmd.PlayedChoThisTurn(player));
                Assert.Equal(1, MelodyCmd.MelodiesThisTurn(player));

                var choKiCard = (TestTagCard)new TestTagCard(CardTag.Cho, CardTag.Ki).MutableClone();
                choKiCard.AssignOwner(player);
                // 只记完成、不记开始的出牌不再算旋律（v0.114.0 删除了 CardPlaysFinished 判定）。
                pcs.RecordCardPlayed(choKiCard, isAutoPlay: true);
                Assert.Equal(1, MelodyCmd.MelodiesThisTurn(player));
                pcs.RecordCardPlayStarted(new CardPlay
                {
                    Card = choKiCard,
                    Player = player,
                    Target = null,
                    ResultPile = PileType.Discard,
                    Resources = new ResourceInfo { EnergyValue = 0, EnergySpent = 0 },
                    IsAutoPlay = true,
                    PlayIndex = 0,
                    PlayCount = 1,
                    PlayOrdinal = 3,
                });
                Assert.Equal(3, MelodyCmd.MelodiesThisTurn(player));

                var turnEndProbe = (MelodyTurnEndProbePower)new MelodyTurnEndProbePower().MutableClone();
                await PowerCmd.Apply(state, turnEndProbe, player.Creature, 1m, player.Creature, null);
                for (int i = 0; i < 5; i++)
                    AddToPile<DefendFool>(player, PileType.Draw);
                await room.Engine.EndPlayerTurnAsync();
                Assert.True(turnEndProbe.ObservedAtPlayerTurnEnd);
                Assert.True(turnEndProbe.PlayedWahAtTurnEnd);
                Assert.Equal(3, turnEndProbe.MelodiesAtTurnEnd);
                Assert.False(MelodyCmd.PlayedWahThisTurn(player));
                Assert.Equal(0, MelodyCmd.MelodiesThisTurn(player));
                break;

            case "adroit_stretch_and_sly_gate":
                StrikeFool adroitAttack = AddToPile<StrikeFool>(player, PileType.Hand);
                await CardCmd.Enchant<Adroit>(adroitAttack, 4m);
                Assert.True(Assert.Single(adroitAttack.Enchantments).GainsBlock);
                await PowerCmd.Apply<StretchPower>(state, player.Creature, 5m, player.Creature, null);

                await room.Engine.PlayCardAsync(player, adroitAttack, enemy);
                Assert.Equal(9, player.Creature.Block);
                Assert.Empty(player.Creature.Powers.OfType<StretchPower>());

                // Non-hand Sly discard must NOT autoplay, while Hand Sly discard DOES autoplay.
                Joy drawSly = AddToPile<Joy>(player, PileType.Draw);
                CardCmd.ApplySingleTurnSly(drawSly);
                int energyBeforeDrawDiscard = pcs.Energy;
                await CardCmd.Discard([drawSly]);
                Assert.Equal(energyBeforeDrawDiscard, pcs.Energy);

                Joy handSly = AddToPile<Joy>(player, PileType.Hand);
                CardCmd.ApplySingleTurnSly(handSly);
                await CardCmd.Discard([handSly]);
                Assert.Equal(energyBeforeDrawDiscard + 1, pcs.Energy);
                break;

            case "autoplay_mod_songbow_aim_theshot":
                Assert.Equal(6, Assert.Single(player.Creature.Powers.OfType<AimPower>()).Amount);
                pcs.GainArrow();
                TheShot shot = AddToPile<TheShot>(player, PileType.Hand);
                int hpBeforeShot = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, shot, enemy);
                // v0.114.0 TheShot: base 20 + Aim 6 = 26 arrow damage; Aim consumed after attack; exhausts.
                Assert.Equal(26, hpBeforeShot - enemy.CurrentHp);
                Assert.Equal(PileType.Exhaust, shot.Pile?.Type);
                Assert.Empty(player.Creature.Powers.OfType<AimPower>());

                // AutoPlayCmd cardModification upgrades drawn card before play.
                foreach (CardModel c in pcs.AllPiles.SelectMany(p => p.Cards).ToList())
                    CardPileCmd.Remove(c);
                DefendFool drawDefend = AddToPile<DefendFool>(player, PileType.Draw);
                int blockBeforeAuto = player.Creature.Block;
                await AutoPlayCmd.FromTopOfDrawPile(state, player, 1, forceExhaust: false,
                    cardModification: card => { if (card.IsUpgradable) card.Upgrade(); });
                Assert.True(drawDefend.IsUpgraded);
                Assert.Equal(8, player.Creature.Block - blockBeforeAuto);

                Sadness sadness = AddToPile<Sadness>(player, PileType.Hand);
                Assert.False(sadness.CanBeGeneratedInCombat);
                Assert.False(sadness.CanPlay(out _));
                break;
        }
    }

    private static T AddToPile<T>(Player player, PileType pile) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pile);
        return card;
    }

    private sealed class TestTagCard(params CardTag[] tags) : CardModel
    {
        public override CardType Type => CardType.Skill;
        public override CardRarity Rarity => CardRarity.Token;
        public override TargetType TargetType => TargetType.Self;
        protected override int CanonicalEnergyCost => 0;
        protected override IReadOnlyCollection<CardTag> CanonicalTags => tags;
    }

    private sealed class DislodgeSourceProbePower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Counter;
        public CardModel? LastSource { get; private set; }

        public override Task AfterArrowDislodged(ImpaledPower power, ArrowSlot arrow, CardModel? source)
        {
            LastSource = source;
            return Task.CompletedTask;
        }
    }

    private sealed class MelodyTurnEndProbePower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Counter;
        public bool ObservedAtPlayerTurnEnd { get; private set; }
        public bool PlayedWahAtTurnEnd { get; private set; }
        public int MelodiesAtTurnEnd { get; private set; }

        public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
        {
            if (side == CombatSide.Player && Owner.Player is { } p)
            {
                ObservedAtPlayerTurnEnd = true;
                PlayedWahAtTurnEnd = MelodyCmd.PlayedWahThisTurn(p);
                MelodiesAtTurnEnd = MelodyCmd.MelodiesThisTurn(p);
            }
            return Task.CompletedTask;
        }
    }
}
