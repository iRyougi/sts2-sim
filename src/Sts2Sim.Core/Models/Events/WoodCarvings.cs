using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 wood-carving event. 偏离 #82：无 Slither 候选时隐藏原版 locked SNAKE UI；
/// #150 的 IsAllowed predicate 已在此实现，事件池选择由 Task 3 集成。</summary>
public sealed class WoodCarvings : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => new([VisibleValue.Int("CarvingCount", 3)], [
        new("BIRD", [VisibleValue.Int("TransformCount", 1), VisibleValue.Condition("TargetFilter", "BasicStarter"), VisibleValue.Id("BirdCard", ModelDb.GetId<Peck>().Entry)])
        { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<Peck>())] },
        new("SNAKE", [VisibleValue.Int("EnchantCount", 1), VisibleValue.Id("SnakeEnchantment", ModelDb.GetId<Slither>().Entry)])
        { Contents = [VisibleContentProjection.ForEnchantment(ModelDb.GetById<Slither>(ModelDb.GetId<Slither>()))] },
        new("TORUS", [VisibleValue.Int("TransformCount", 1), VisibleValue.Condition("TargetFilter", "BasicStarter"), VisibleValue.Id("ToricCard", ModelDb.GetId<ToricToughness>().Entry)])
        { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<ToricToughness>())] },
    ]);

    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.Deck.Cards.Any(card => card.Rarity == CardRarity.Basic && card.IsRemovable));

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new List<EventOption>
        {
            new EventOption("BIRD", BirdAsync),
        };
        Slither slither = ModelDb.GetById<Slither>(ModelDb.GetId<Slither>());
        if (Owner.Deck.Cards.Any(slither.CanEnchant))
        {
            options.Add(new EventOption("SNAKE", SnakeAsync));
        }
        options.Add(new EventOption("TORUS", TorusAsync));
        return options.AsReadOnly();
    }

    private static bool IsBasicTransformable(CardModel card) =>
        card.Rarity == CardRarity.Basic && card.IsTransformable;

    /// <summary>原版 <c>Bird</c> / <c>Torus</c>：<c>CardSelectCmd.FromDeckGeneric(Owner, prefs(1), IsTransformable &amp;&amp; Basic)</c>，
    /// 牌组顺序、无排序，由玩家选 1 张。</summary>
    private Task<CardModel?> SelectBasicTransformableAsync() =>
        SelectOneAsync(Owner.Deck.Cards.Where(IsBasicTransformable));

    private async Task<CardModel?> SelectOneAsync(IEnumerable<CardModel> candidates) =>
        (await CardSelectCmd.SelectCardsAsync(RunState, Owner, candidates, 1, 1, this)).FirstOrDefault();

    private async Task BirdAsync()
    {
        CardModel? card = await SelectBasicTransformableAsync();
        if (card is not null)
        {
            await CardCmd.TransformTo<Peck>(card, RunState);
        }

        Finish();
    }

    private async Task SnakeAsync()
    {
        Slither slither = ModelDb.GetById<Slither>(ModelDb.GetId<Slither>());
        // 原版 Snake：CardSelectCmd.FromDeckForEnchantment(Owner, Slither, 1, prefs(1))，牌组顺序。
        CardModel? card = await SelectOneAsync(Owner.Deck.Cards.Where(slither.CanEnchant));
        if (card is not null)
        {
            await CardCmd.Enchant<Slither>(card, 1m);
        }

        Finish();
    }

    private async Task TorusAsync()
    {
        CardModel? card = await SelectBasicTransformableAsync();
        if (card is not null)
        {
            await CardCmd.TransformTo<ToricToughness>(card, RunState);
        }

        Finish();
    }
}
