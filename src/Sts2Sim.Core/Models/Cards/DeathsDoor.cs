using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>DeathsDoor</c>：1 费技能，获得 6（升级 7）格挡；本回合持有者施加过灾厄时额外再获得 2 次。</summary>
public sealed class DeathsDoor : CardModel, ICardChoiceBaseValueProvider
{
    private const int Repeat = 2;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool GainsBlock => true;

    protected override int CanonicalEnergyCost => 1;

    private decimal Block => IsUpgraded ? 7m : 6m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    private bool WasDoomAppliedThisTurn =>
        CombatState is CombatState concrete &&
        concrete.SemanticHistory.CountThisTurn(concrete, CombatSemanticHistory.ActorEvent.DoomApplied, Owner) > 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int blockGains = 1;
        if (WasDoomAppliedThisTurn)
            blockGains += Repeat;
        for (int index = 0; index < blockGains; index++)
            await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
    }
}
