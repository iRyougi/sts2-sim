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

    private async Task BirdAsync()
    {
        CardModel? card = Owner.Deck.Cards.FirstOrDefault(IsBasicTransformable);
        if (card is not null)
        {
            await CardCmd.TransformTo<Peck>(card, RunState);
        }

        Finish();
    }

    private async Task SnakeAsync()
    {
        Slither slither = ModelDb.GetById<Slither>(ModelDb.GetId<Slither>());
        CardModel? card = Owner.Deck.Cards.FirstOrDefault(slither.CanEnchant);
        if (card is not null)
        {
            await CardCmd.Enchant<Slither>(card, 1m);
        }

        Finish();
    }

    private async Task TorusAsync()
    {
        CardModel? card = Owner.Deck.Cards.FirstOrDefault(IsBasicTransformable);
        if (card is not null)
        {
            await CardCmd.TransformTo<ToricToughness>(card, RunState);
        }

        Finish();
    }
}
