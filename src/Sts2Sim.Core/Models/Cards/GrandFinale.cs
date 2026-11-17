using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Grand Finale.</summary>
public sealed class GrandFinale : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 60m;
    // 原版 ShouldGlowGoldInternal => IsPlayable（抽牌堆为空）；模拟器用 ShouldPlay 实现可打出，这里按同一条件。
    protected override bool ShouldGlowGoldInternal => Owner.PlayerCombatState!.DrawPile.Cards.Count == 0;

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
