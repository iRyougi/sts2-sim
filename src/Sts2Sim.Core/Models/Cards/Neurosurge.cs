using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Neurosurge</c>：获得 3（升级 4）能量，抽 2，然后获得 3 层 <see cref="NeurosurgePower"/>
/// （之后每个己方回合开始给自己施加 Doom）。</summary>
public sealed class Neurosurge : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;

    private int Energy => IsUpgraded ? 4 : 3;

    private const int Cards = 2;

    private const int NeurosurgeAmount = 3;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards, Energy: Energy);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(Energy, Owner);
        await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
        await PowerCmd.Apply<NeurosurgePower>(CombatState!, Owner.Creature, NeurosurgeAmount, Owner.Creature, this);
    }
}
