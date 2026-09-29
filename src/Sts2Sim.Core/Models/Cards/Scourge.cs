using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Scourge</c>：1 费，给一名敌人 13（升级 16）层 Doom，然后抽 1（升级 2）张。</summary>
public sealed class Scourge : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    private decimal Doom => IsUpgraded ? 16m : 13m;

    private int Cards => IsUpgraded ? 2 : 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<DoomPower>(CombatState!, cardPlay.Target, Doom, Owner.Creature, this);
        await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
    }
}
