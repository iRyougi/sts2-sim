using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得能量并抽牌，同时施加限制本回合再出一张牌的 EncorePower（原版 <c>Encore</c>）。</summary>
public sealed class Encore : CardModel, ICardChoiceBaseValueProvider
{
    private const int Energy = 1;
    private const decimal EncoreAmount = 1m;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    private int Cards => IsUpgraded ? 3 : 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards, Energy: Energy);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(Energy, Owner);
        await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
        await PowerCmd.Apply<EncorePower>(CombatState!, Owner.Creature, EncoreAmount, Owner.Creature, this);
    }
}
