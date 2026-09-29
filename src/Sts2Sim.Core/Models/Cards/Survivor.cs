using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Gains 8 block, then selects and discards one card from hand; upgrade increases block by 3.</summary>
public sealed class Survivor : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 8m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Basic;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override void OnUpgrade() => _block += 3m;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(
            CombatState!,
            Owner.Creature,
            _block,
            ValueProp.Move,
            this,
            cardPlay);
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHandForDiscard(
            CombatState!,
            Owner,
            count: 1,
            source: this);
        if (selected.Count > 0)
        {
            await CardCmd.Discard(selected);
        }
    }
}
