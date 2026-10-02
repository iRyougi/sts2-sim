using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>抽牌堆选2张转化为 MinionDiveBomb。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Charge</c>）</summary>
public sealed class Charge : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IReadOnlyList<CardModel> selection = await CardSelectCmd.FromCombatPile(CombatState!, Owner,
            Owner.PlayerCombatState!.DrawPile, 2, 2, this, cancelable: false);
        foreach (CardModel card in selection)
        {
            MinionDiveBomb replacement = await CardCmd.CreateAndTransform<MinionDiveBomb>(card);
            if (IsUpgraded)
            {
                replacement.Upgrade();
            }
        }
    }
}
