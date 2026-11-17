using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>施加 MythicFormPower；不可在战斗中随机生成（原版 <c>MythicForm</c>）。</summary>
public sealed class MythicForm : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => 3;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        MythicFormPower? power = await PowerCmd.Apply<MythicFormPower>(
            CombatState!, Owner.Creature, 1m, Owner.Creature, this);
        if (IsUpgraded)
        {
            power?.IncrementUpgraded();
        }
    }
}
