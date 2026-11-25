using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得 8（升级 11）点格挡，并把手中随机 1 张 Sadness 变形为本牌的克隆（v0.114.0 原版 <c>Growth</c>）。</summary>
public sealed class Growth : CardModel, ICardChoiceBaseValueProvider
{
    public override bool GainsBlock => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    private decimal Block => IsUpgraded ? 11m : 8m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        List<Sadness> sadnessCards = Owner.PlayerCombatState!.Hand.Cards.OfType<Sadness>().ToList();
        if (sadnessCards.Count != 0)
        {
            sadnessCards.StableShuffle(Owner.RunState.Rng.CombatCardSelection);
            await CardCmd.Transform(sadnessCards[0], CreateClone());
        }
    }
}
