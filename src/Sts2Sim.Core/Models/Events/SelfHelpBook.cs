using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Events;

public sealed class SelfHelpBook : EventModel
{
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("READ_THE_BACK", [VisibleValue.Id("EnchantmentId", "SHARP"), VisibleValue.Int("Amount", 2), VisibleValue.Scope("CardType", "Attack"), VisibleValue.Int("SelectCount", 1), VisibleValue.Int("AdditionalDamage", 2)]),
        new("READ_PASSAGE", [VisibleValue.Id("EnchantmentId", "NIMBLE"), VisibleValue.Int("Amount", 2), VisibleValue.Scope("CardType", "Skill"), VisibleValue.Int("SelectCount", 1), VisibleValue.Int("AdditionalBlock", 2)]),
        new("READ_ENTIRE_BOOK", [VisibleValue.Id("EnchantmentId", "SWIFT"), VisibleValue.Int("Amount", 2), VisibleValue.Scope("CardType", "Power"), VisibleValue.Int("SelectCount", 1), VisibleValue.Int("Draw", 2), VisibleValue.Condition("DrawTrigger", "FirstPlay")]),
        new("READ_THE_BACK_LOCKED", [VisibleValue.Condition("RequiredCardType", "Attack")]),
        new("READ_PASSAGE_LOCKED", [VisibleValue.Condition("RequiredCardType", "Skill")]),
        new("READ_ENTIRE_BOOK_LOCKED", [VisibleValue.Condition("RequiredCardType", "Power")]),
    ]);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        var options = new[]
        {
            Option<Sharp>("READ_THE_BACK", CardType.Attack),
            Option<Nimble>("READ_PASSAGE", CardType.Skill),
            Option<Swift>("READ_ENTIRE_BOOK", CardType.Power),
        };
        return options.All(o => o.IsLocked)
            ? [new("NO_OPTIONS", () => { Finish(); return Task.CompletedTask; })] : options;
    }

    private EventOption Option<T>(string key, CardType type) where T : EnchantmentModel
    {
        var enchantment = ModelDb.GetById<T>(ModelDb.GetId<T>());
        bool available = Owner.Deck.Cards.Any(c => c.Type == type && enchantment.CanEnchant(c));
        return new EventOption(available ? key : key + "_LOCKED", available ? () => Enchant<T>(type) : null);
    }

    private async Task Enchant<T>(CardType type) where T : EnchantmentModel
    {
        var enchantment = ModelDb.GetById<T>(ModelDb.GetId<T>());
        var cards = await CardSelectCmd.SelectCardsAsync(RunState, Owner,
            Owner.Deck.Cards.Where(c => c.Type == type && enchantment.CanEnchant(c)), 1, 1, this);
        foreach (var card in cards) await CardCmd.Enchant<T>(card, 2m);
        Finish();
    }
}
