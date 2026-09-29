using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models.Cards;

public sealed class DrumOfBattle : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: 2, Energy: _energy);

    private int _energy = 2;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override Task OnPlay(CardPlay cardPlay) =>
        CardPileCmd.Draw(CombatState!, 2, Owner, fromHandDraw: false);

    public override async Task AfterCardExhausted(CardModel card, bool causedByEthereal)
    {
        if (ReferenceEquals(card, this) && CombatState is not null)
        {
            int playCount = BaseReplayCount + 1;
            foreach (EnchantmentModel enchantment in Enchantments)
            {
                playCount = enchantment.EnchantPlayCount(playCount);
            }
            playCount = Hook.ModifyCardPlayCount(CombatState, this, playCount, null,
                out List<AbstractModel> modifiers);
            await Hook.AfterModifyingCardPlayCount(CombatState, this, modifiers);
            for (int i = 0; i < playCount; i++)
            {
                await PlayerCmd.GainEnergy(_energy, Owner);
            }
        }
    }

    protected override void OnUpgrade() => _energy++;
}
