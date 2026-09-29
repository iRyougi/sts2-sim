using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>选1张手牌转化为 MinionStrike。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Begone</c>）</summary>
public sealed class Begone : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CardModel? selected = (await CardSelectCmd.FromHand(CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards.Where(c => c != this), 1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is null)
        {
            return;
        }

        MinionStrike replacement = await CardCmd.CreateAndTransform<MinionStrike>(selected);
        if (IsUpgraded)
        {
            replacement.Upgrade();
        }
    }
}
