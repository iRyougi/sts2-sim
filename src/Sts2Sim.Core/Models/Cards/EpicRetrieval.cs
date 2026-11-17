using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>当本卡直接拔除己方箭矢时获得能量（原版 <c>EpicRetrieval</c>）。</summary>
public sealed class EpicRetrieval : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    private decimal Damage => IsUpgraded ? 20m : 15m;
    private int Energy => IsUpgraded ? 4 : 3;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Energy: Energy);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override bool ShouldGlowGoldInternal => CombatState?.HittableEnemies.Any(NextHitWillDislodgeArrowFrom) ?? false;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    public override async Task AfterArrowDislodged(ImpaledPower power, ArrowSlot arrow, CardModel? source)
    {
        if (ReferenceEquals(power.Applier, Owner.Creature) && ReferenceEquals(source, this))
        {
            await PlayerCmd.GainEnergy(Energy, Owner);
        }
    }

    private bool NextHitWillDislodgeArrowFrom(Creature creature)
    {
        ImpaledPower? impaledPower = creature.Powers.OfType<ImpaledPower>().FirstOrDefault(p => ReferenceEquals(p.Applier, Owner.Creature));
        if (impaledPower == null)
        {
            return false;
        }
        return impaledPower.HitCounter == 1;
    }
}
