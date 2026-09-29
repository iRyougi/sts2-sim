using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Guards 转化生成的随从卡。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.MinionSacrifice</c>）：
/// 0费,获得7点格挡,Exhaust。</summary>
public sealed class MinionSacrifice : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 7m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
    }

    protected override void OnUpgrade() => _block += 3m;
}
