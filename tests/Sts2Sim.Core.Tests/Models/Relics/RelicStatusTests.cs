using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Runs.Transplant;
using System.Reflection;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

file sealed class StatusAttackCard : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class StatusProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.None;

    public void SetStatus(RelicStatus status) => Status = status;
}

[Collection("ModelDb")]
public sealed class RelicStatusTests : IDisposable
{
    public RelicStatusTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes
            .Append(typeof(StatusProbeRelic))
            .Append(typeof(StatusAttackCard)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Status_DefaultsToNormal_AndSurvivesClone()
    {
        var runState = new RunState("relic-status-clone", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        await RelicCmd.Obtain(ModelDb.Relic<StatusProbeRelic>(), player);
        var relic = Assert.Single(player.Relics.OfType<StatusProbeRelic>());
        Assert.Equal(RelicStatus.Normal, relic.Status);

        relic.SetStatus(RelicStatus.Active);

        foreach ((string label, RunState clone) in new[]
        {
            ("CloneExact", runState.CloneExact()),
            ("CloneReseeded(1)", runState.CloneReseeded(1)),
        })
        {
            var cloned = Assert.Single(clone.Players[0].Relics.OfType<StatusProbeRelic>());
            Assert.True(RelicStatus.Active == cloned.Status, label);
        }

        // Saved-property restore (transplant / act-entry snapshot) writes fields directly; the source setters
        // ([SavedProperty] AttacksPlayed, GainEnergyInNextCombat) recompute Status on load, so Apply must too.
        foreach ((Type type, string field, object value) in new (Type, string, object)[]
        {
            (typeof(PenNib), "_attacksPlayed", 9),
            (typeof(VenerableTeaSet), "_isArmed", true),
        })
        {
            await RelicCmd.Obtain((RelicModel)ModelDb.Get(type), player);
            RelicModel saved = player.Relics.Single(candidate => candidate.GetType() == type);
            type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(saved, value);
            var restored = (RelicModel)ModelDb.Get(type).MutableClone();
            restored.AssignOwner(player);
            TransplantSavedProperties.Apply(restored, TransplantSavedProperties.Export(saved, type.Name), type.Name, relic: true);
            Assert.True(RelicStatus.Active == restored.Status, $"{type.Name} restored from saved properties");
        }

        // Player.MeltRelic (Player.cs:531) sets Disabled together with IsMelted.
        relic.IsWax = true;
        await RelicCmd.Melt(relic);
        Assert.Equal(RelicStatus.Disabled, relic.Status);
    }

    /// <summary>Each row is one trigger shape. Expected values are written from the v0.113.1 source
    /// (MegaCrit.Sts2.Core.Models.Relics.*), not from the simulator's previous behaviour.</summary>
    [Theory]
    [InlineData(typeof(Kunai))]            // Kunai.UpdateDisplay: AttacksPlayedThisTurn % 3 == 2 -> Active; reset on turn start / combat end
    [InlineData(typeof(HappyFlower))]      // HappyFlower.BeforeSideTurnStart (v0.114.0): TurnsSeen == 2 -> Active; AfterCombatEnd -> Normal
    [InlineData(typeof(RedSkull))]         // RedSkull.ModifyStrengthIfNecessary: hp <= 50% -> Active; AfterCombatEnd -> Normal
    [InlineData(typeof(PenNib))]           // PenNib.UpdateDisplay: AttacksPlayed % 10 == 9 -> Active; no combat-end reset
    [InlineData(typeof(VenerableTeaSet))]  // VenerableTeaSet.GainEnergyInNextCombat setter: armed -> Active
    [InlineData(typeof(ArtOfWar))]         // ArtOfWar: Active at AfterEnergyReset, Normal once an attack is played
    public async Task Status_FollowsSourceTransitions(Type relicType)
    {
        var runState = new RunState("relic-status-" + relicType.Name, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        await RelicCmd.Obtain((RelicModel)ModelDb.Get(relicType), player);
        RelicModel relic = player.Relics.Single(candidate => candidate.GetType() == relicType);
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());

        void Expect(RelicStatus expected, string step)
        {
            Assert.True(expected == relic.Status, $"{relicType.Name} {step}: expected {expected}, got {relic.Status}");
            bool projectedActive = VisibleContentProjection.ForRelic(relic).Values
                .Any(value => value.Name == "State" && value.Value == "active");
            Assert.True(projectedActive == (expected == RelicStatus.Active),
                $"{relicType.Name} {step}: ForRelic State=active was {projectedActive}");
        }

        async Task PlayAttacks(int count)
        {
            for (int index = 0; index < count; index++)
            {
                var card = (CardModel)ModelDb.Card<StatusAttackCard>().MutableClone();
                card.AssignOwner(player);
                CardPileCmd.Add(card, PileType.Hand);
                await room.Engine.PlayCardAsync(player, card, room.Engine.State.Enemies[0]);
            }
        }

        switch (relicType.Name)
        {
            case nameof(Kunai):
                await room.Enter(runState);
                Expect(RelicStatus.Normal, "turn 1 start");
                await PlayAttacks(2);
                Expect(RelicStatus.Active, "after 2 attacks");
                await PlayAttacks(1);
                Expect(RelicStatus.Normal, "after 3rd attack (triggered)");
                await PlayAttacks(2);
                Expect(RelicStatus.Active, "after 5 attacks");
                await room.Engine.EndPlayerTurnAsync();
                Expect(RelicStatus.Normal, "next turn start");
                break;
            case nameof(HappyFlower):
                await room.Enter(runState);
                Expect(RelicStatus.Normal, "turn 1 (TurnsSeen 1)");
                await room.Engine.EndPlayerTurnAsync();
                Expect(RelicStatus.Active, "turn 2 (TurnsSeen 2)");
                await room.Engine.EndPlayerTurnAsync();
                Expect(RelicStatus.Normal, "turn 3 (TurnsSeen wraps to 0, energy gained)");
                await room.Engine.EndPlayerTurnAsync();
                await room.Engine.EndPlayerTurnAsync();
                Expect(RelicStatus.Active, "turn 5 (TurnsSeen 2 again)");
                break;
            case nameof(RedSkull):
                await room.Enter(runState);
                int half = player.Creature.MaxHp / 2;
                Expect(RelicStatus.Normal, "full hp");
                await CreatureCmd.Damage(room.Engine.State, [player.Creature],
                    player.Creature.MaxHp - half - 1, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
                Expect(RelicStatus.Normal, "just above half");
                await CreatureCmd.Damage(room.Engine.State, [player.Creature],
                    1, ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
                Expect(RelicStatus.Active, "at or below half");
                await CreatureCmd.Heal(player.Creature, player.Creature.MaxHp);
                Expect(RelicStatus.Normal, "healed above half");
                break;
            case nameof(PenNib):
                await room.Enter(runState);
                Expect(RelicStatus.Normal, "start");
                await PlayAttacks(9);
                Expect(RelicStatus.Active, "after 9 attacks");
                await PlayAttacks(1);
                Expect(RelicStatus.Normal, "after 10th attack (doubled, counter wraps)");
                await PlayAttacks(9);
                Expect(RelicStatus.Active, "after 19 attacks");
                break;
            case nameof(VenerableTeaSet):
                Expect(RelicStatus.Normal, "before rest site");
                await new RestSiteRoom().Enter(runState);
                Expect(RelicStatus.Active, "armed at rest site");
                await new MerchantRoom().Enter(runState);
                Expect(RelicStatus.Active, "still armed in a non-combat room");
                await room.Enter(runState);
                Expect(RelicStatus.Normal, "energy granted at combat start");
                break;
            case nameof(ArtOfWar):
                await room.Enter(runState);
                Expect(RelicStatus.Active, "turn 1 energy reset");
                await PlayAttacks(1);
                Expect(RelicStatus.Normal, "attack played this turn");
                await room.Engine.EndPlayerTurnAsync();
                Expect(RelicStatus.Active, "turn 2 energy reset");
                break;
            default:
                throw new InvalidOperationException(relicType.Name);
        }
    }
}
