using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Cards;

public sealed class TeslaCoil : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 3m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        List<LightningOrb> lightning = Owner.PlayerCombatState!.OrbQueue.Orbs.OfType<LightningOrb>().ToList();
        foreach (LightningOrb orb in lightning)
        {
            await OrbCmd.Passive(CombatState!, orb, cardPlay.Target);
            if (IsUpgraded) await OrbCmd.Passive(CombatState!, orb, cardPlay.Target);
        }
    }

    protected override void OnUpgrade() => _damage += 1m;
}
