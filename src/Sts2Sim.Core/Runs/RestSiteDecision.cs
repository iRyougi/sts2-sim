using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Runs;

/// <summary>休息点的一次玩家选择。</summary>
public abstract record RestSiteDecision
{
    protected RestSiteDecision()
    {
    }

    /// <summary>Stable content identifier used when presenting the option.</summary>
    public abstract string OptionId { get; }

    /// <summary>Whether this snapshot option can currently be selected.</summary>
    public virtual bool IsEnabled => true;

    /// <summary>Lower values are chosen first; equal priorities preserve snapshot order.</summary>
    public abstract int Priority { get; }

    public virtual string GetLabel(Player player) => OptionId;

    public abstract Task ExecuteAsync(Player player);

    public sealed record Heal : RestSiteDecision
    {
        public override string OptionId => "heal";
        public override int Priority => 3;

        public override async Task ExecuteAsync(Player player)
        {
            decimal healAmount = player.Creature.MaxHp * 0.3m;
            healAmount = Hooks.Hook.ModifyRestSiteHealAmount(
                player.RunState,
                player.Creature,
                healAmount);
            await CreatureCmd.Heal(player.Creature, healAmount);
            await Hooks.Hook.AfterRestSiteHeal(player.RunState, player, isMimicked: false);
            var rewards = new List<Reward>();
            Hooks.Hook.ModifyRestSiteHealRewards(player.RunState, player, rewards, isMimicked: false);
            await RewardsCmd.OfferCustom(player, rewards);
        }
    }

    public sealed record Smith(CardModel Card) : RestSiteDecision
    {
        public override bool IsEnabled => Card.IsUpgradable;
        public override string OptionId => "smith";
        public override int Priority => 2;

        public override string GetLabel(Player player)
        {
            int deckIndex = player.Deck.Cards
                .Select((card, index) => (card, index))
                .Where(candidate => ReferenceEquals(candidate.card, Card))
                .Select(candidate => candidate.index)
                .DefaultIfEmpty(-1)
                .Single();
            if (deckIndex < 0)
            {
                throw new InvalidOperationException("Rest-site Smith candidate is not in the player's deck.");
            }

            return $"smith:{deckIndex}:{Card.Id}";
        }

        public override Task ExecuteAsync(Player player)
        {
            if (!player.Deck.Cards.Any(card => ReferenceEquals(card, Card)))
            {
                throw new InvalidOperationException("Card to smith is not in the player's deck.");
            }
            if (!Card.IsUpgradable)
            {
                throw new InvalidOperationException("Card to smith is not upgradable.");
            }
            CardCmd.Upgrade(Card);
            return Task.CompletedTask;
        }
    }

    public sealed record Hatch : RestSiteDecision
    {
        public override string OptionId => "hatch";
        public override int Priority => 0;

        public override async Task ExecuteAsync(Player player)
        {
            if (!player.Deck.Cards.Any(card => card is Models.Cards.ByrdonisEgg))
            {
                throw new InvalidOperationException("Hatch requires a ByrdonisEgg in the player's persistent deck.");
            }
            await RelicCmd.Obtain(ModelDb.Relic<Models.Relics.Byrdpip>(), player);
        }
    }

    public sealed record Cook : RestSiteDecision
    {
        public override string OptionId => "cook";
        public override int Priority => 1;

        public override async Task ExecuteAsync(Player player)
        {
            IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
                player.RunState,
                player,
                player.Deck.Cards.Where(card => card.IsRemovable),
                minCount: 2,
                maxCount: 2,
                source: null,
                cancelable: true);
            if (selected.Count == 0)
            {
                return;
            }
            foreach (CardModel card in selected)
            {
                await CardPileCmd.RemoveFromDeck(player, card);
            }
            await CreatureCmd.GainMaxHp(player.Creature, 5m);
        }
    }
}
