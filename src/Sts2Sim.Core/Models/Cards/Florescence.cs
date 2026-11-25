using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>选 1 张手牌变形为 TheShot（本牌升级时 TheShot 也升级）（v0.114.0 原版 <c>Florescence</c>）。</summary>
public sealed class Florescence : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CardModel? selected = (await CardSelectCmd.FromHand(
            CombatState!,
            Owner,
            Owner.PlayerCombatState!.Hand.Cards,
            1,
            1,
            this,
            cancelable: false)).FirstOrDefault();
        if (selected is null)
        {
            return;
        }

        var theShot = (CardModel)ModelDb.Card<TheShot>().MutableClone();
        theShot.AssignOwner(Owner);
        if (IsUpgraded)
        {
            CardCmd.Upgrade(theShot);
        }
        await CardCmd.Transform(selected, theShot);
    }
}
