using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ArchaicTooth : RelicModel
{
    private static IReadOnlyDictionary<ModelId, CardModel> TranscendenceUpgrades =>
        new Dictionary<ModelId, CardModel>
        {
            [ModelDb.Card<Bash>().Id] = ModelDb.Card<Break>(),
            [ModelDb.Card<Neutralize>().Id] = ModelDb.Card<Suppress>(),
            [ModelDb.Card<Unleash>().Id] = ModelDb.Card<Protector>(),
            [ModelDb.Card<FallingStar>().Id] = ModelDb.Card<MeteorShower>(),
            [ModelDb.Card<Dualcast>().Id] = ModelDb.Card<Quadcast>(),
        };

    public static IReadOnlyList<CardModel> TranscendenceCards => TranscendenceUpgrades.Values.ToList();

    /// <summary>原版 <c>GetTranscendenceStarterCard</c> 的判定：Orobas 据此决定是否提供本遗物。</summary>
    internal static bool IsTranscendenceStarter(CardModel card) => TranscendenceUpgrades.ContainsKey(card.Id);

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool HasUponPickupEffect => true;

    public override async Task AfterObtained()
    {
        IReadOnlyDictionary<ModelId, CardModel> upgrades = TranscendenceUpgrades;
        CardModel? original = Owner.Deck.Cards.FirstOrDefault(card => upgrades.ContainsKey(card.Id));
        if (original is not null)
        {
            await TransformPreservingCardState(original, upgrades[original.Id]);
        }
    }

    private static async Task TransformPreservingCardState(CardModel original, CardModel canonical)
    {
        var replacement = (CardModel)canonical.MutableClone();
        if (original.IsUpgraded)
        {
            replacement.Upgrade();
        }

        if (original.Enchantments.SingleOrDefault() is EnchantmentModel enchantment)
        {
            var enchantmentClone = (EnchantmentModel)enchantment.MutableClone();
            await CardCmd.Enchant(enchantmentClone, replacement, enchantment.Magnitude);
        }

        await CardCmd.Transform(original, replacement);
    }
}
