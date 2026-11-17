using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

file sealed class GlowAttackCard : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
}

[Collection("ModelDb")]
public sealed class CardGlowTests : IDisposable
{
    public CardGlowTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Append(typeof(GlowAttackCard)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    /// <summary>Expected values follow the v0.113.1 source: Impatience.ShouldGlowGoldInternal (no Attack in hand),
    /// Normality.ShouldGlowRedInternal (CardsPlayedThisTurn &gt;= 3). The holder gate (NHandCardHolder.ShouldGlowGold:
    /// CanPlay and Phase == Play; ShouldGlowRed: Phase == Play) is what ForCard exposes.</summary>
    [Theory]
    [InlineData("Impatience", true)]
    [InlineData("Impatience", false)]
    [InlineData("Normality", true)]
    [InlineData("Normality", false)]
    public async Task Glow_FollowsSourceCondition_AndProjection(string cardName, bool conditionHolds)
    {
        var runState = new RunState("card-glow-" + cardName + conditionHolds, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        PlayerCombatState combat = player.PlayerCombatState!;
        bool gold = cardName == "Impatience";

        CardModel Make(CardModel canonical)
        {
            var card = (CardModel)canonical.MutableClone();
            card.AssignOwner(player);
            return card;
        }

        foreach (CardModel card in combat.Hand.Cards.ToList())
            CardPileCmd.Add(card, PileType.Discard);
        CardModel subject = Make(cardName == "Impatience" ? ModelDb.Card<Impatience>() : ModelDb.Card<Normality>());
        CardPileCmd.Add(subject, PileType.Hand);

        if (gold && !conditionHolds)
            CardPileCmd.Add(Make(ModelDb.Card<GlowAttackCard>()), PileType.Hand);
        if (!gold && conditionHolds)
        {
            for (int index = 0; index < 3; index++)
            {
                CardModel attack = Make(ModelDb.Card<GlowAttackCard>());
                CardPileCmd.Add(attack, PileType.Hand);
                await room.Engine.PlayCardAsync(player, attack, room.Engine.State.Enemies[0]);
            }
        }

        bool actual = gold ? subject.ShouldGlowGold : subject.ShouldGlowRed;
        Assert.True(conditionHolds == actual, $"{cardName}: model glow expected {conditionHolds}");
        VisibleContent content = VisibleContentProjection.ForCard(subject);
        int Read(string name) => int.Parse(content.Values.Single(value => value.Name == name).Value);
        Assert.True((conditionHolds ? 1 : 0) == Read(gold ? "GlowGold" : "GlowRed"), $"{cardName}: projected glow");
        Assert.Equal(0, Read(gold ? "GlowRed" : "GlowGold"));

        // NHandCardHolder.UpdateCard only computes glow while CombatManager.IsInProgress: once the combat has ended
        // the projection must not report glow for a card whose own condition still holds.
        if (conditionHolds)
        {
            await CreatureCmd.Kill(room.Engine.State.Enemies[0]);
            Assert.True(room.Engine.CheckWinCondition());
            content = VisibleContentProjection.ForCard(subject);
            Assert.True(0 == Read(gold ? "GlowGold" : "GlowRed"), $"{cardName}: glow after combat ended");
        }
    }
}
