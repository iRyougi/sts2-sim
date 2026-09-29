using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class CompileDriver : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 7m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        int distinctOrbs = Owner.PlayerCombatState!.OrbQueue.Orbs.Select(orb => orb.Id).Distinct().Count();
        await CardPileCmd.Draw(CombatState!, distinctOrbs, Owner, fromHandDraw: false);
    }
    protected override void OnUpgrade() => _damage += 3m;
}
