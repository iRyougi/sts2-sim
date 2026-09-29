using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>9格挡;出牌后进手牌而非弃牌堆。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.ParticleWall</c>）。</summary>
public sealed class ParticleWall : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 9m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;

    protected override int CanonicalStarCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
    }

    protected override CardLocation GetResultLocationForCardPlay()
    {
        CardLocation location = base.GetResultLocationForCardPlay();
        return location.PileType == PileType.Discard ? location with { PileType = PileType.Hand } : location;
    }

    protected override void OnUpgrade() => _block += 3m;
}
