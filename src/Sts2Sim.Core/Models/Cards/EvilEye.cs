using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class EvilEye : CardModel, ICardChoiceBaseValueProvider
{
    // 原版 WasCardExhaustedThisTurn 查 CardExhaustedEntry(HappenedThisTurn && Actor == Owner.Creature)；
    // 与 OnPlay 一致，用 SemanticHistory 的等价计数。
    protected override bool ShouldGlowGoldInternal => WasCardExhaustedThisTurn;

    private bool WasCardExhaustedThisTurn =>
        CombatState is CombatState concrete && concrete.CountCardsExhaustedThisTurn(Owner) > 0;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)_block);

    private decimal _block = 8m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    public override bool GainsBlock => true;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int gains = CombatState is CombatState concrete &&
            concrete.CountCardsExhaustedThisTurn(Owner) > 0 ? 2 : 1;
        for (int i = 0; i < gains; i++)
        {
            await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        }
    }

    protected override void OnUpgrade() => _block += 3m;
}
