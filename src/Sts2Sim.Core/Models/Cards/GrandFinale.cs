using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Grand Finale. UI gold glow is omitted because the simulator has no glow hook.</summary>
public sealed class GrandFinale : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 60m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 0;
    public override bool ShouldPlay(CardModel card, bool isAutoPlay) =>
        !ReferenceEquals(card, this) || Owner.PlayerCombatState!.DrawPile.Cards.Count == 0;
    protected override Task OnPlay(CardPlay cardPlay) => DamageCmd.Attack(_damage).FromCard(this, cardPlay)
        .TargetingAllOpponents(CombatState!).Execute();
    protected override void OnUpgrade() => _damage += 15m;
}
