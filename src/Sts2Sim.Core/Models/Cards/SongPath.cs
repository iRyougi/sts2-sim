using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>每回合首次打出 Cho 时获得格挡（原版 <c>SongPath</c>）。</summary>
public sealed class SongPath : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    private decimal PowerAmount => IsUpgraded ? 7m : 5m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<SongPathPower>(CombatState!, Owner.Creature, PowerAmount, Owner.Creature, this);
    }
}
