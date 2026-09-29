using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Deviation #160: the simulator omits Byrdpip's Godot attacker animation while retaining the
/// source-exact card damage and targeting mechanics.
/// </summary>
public sealed class ByrdSwoop : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 14m;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target)
            .Execute();
    }

    protected override void OnUpgrade() => _damage += 4m;
}
