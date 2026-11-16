using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>v0.113.1 原版 <c>ExpectAFight</c>：获得 15(20) 点格挡，手牌中每张非 X 费攻击牌费用 −1 直到打出。</summary>
public sealed class ExpectAFight : CardModel, ICardChoiceBaseValueProvider
{
    private const int EnergyReduction = 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)_block, Energy: EnergyReduction);

    private decimal _block = 15m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    public override bool GainsBlock => true;
    protected override int CanonicalEnergyCost => 3;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        List<CardModel> attacks = Owner.PlayerCombatState!.Hand.Cards
            .Where(card => card.Type == CardType.Attack)
            .ToList();
        foreach (CardModel attack in attacks)
        {
            if (!attack.CostsXEnergy)
            {
                attack.AddEnergyCostUntilPlayed(-EnergyReduction);
            }
        }
    }

    protected override void OnUpgrade() => _block += 5m;
}
