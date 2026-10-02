using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>6格挡,弃牌堆选1张放回抽牌堆顶。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.CosmicIndifference</c>）</summary>
public sealed class CosmicIndifference : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 6m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        CardModel? selected = (await CardSelectCmd.FromCombatPile(CombatState!, Owner,
            Owner.PlayerCombatState!.DiscardPile, 1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is not null)
        {
            CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
        }
    }

    protected override void OnUpgrade() => _block += 3m;
}
