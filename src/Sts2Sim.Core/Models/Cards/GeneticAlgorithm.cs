using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class GeneticAlgorithm : CardModel, ICardChoiceBaseValueProvider
{
    private int _currentBlock = 1;
    private int _increasedBlock;
    private int _increase = 3;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override bool GainsBlock => true;
    public int CurrentBlock => _currentBlock;
    public int IncreasedBlock => _increasedBlock;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: _currentBlock);

    protected override async Task OnPlay(CardPlay play)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _currentBlock, ValueProp.Move, this, play);
        BuffFromPlay(_increase);
        if (DeckVersion is GeneticAlgorithm persistent)
            persistent.BuffFromPlay(_increase);
    }

    private void BuffFromPlay(int amount)
    {
        _increasedBlock += amount;
        _currentBlock = 1 + _increasedBlock;
    }

    protected override void OnUpgrade() => _increase += 1;

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context)
    {
        builder.Append(_currentBlock);
        builder.Append(_increasedBlock);
        builder.Append(_increase);
    }
}
