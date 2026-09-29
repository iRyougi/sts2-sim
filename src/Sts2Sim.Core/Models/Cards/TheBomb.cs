using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class TheBomb : CardModel
{
    private decimal _damage = 40m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 2;

    protected override void OnUpgrade() => _damage += 10m;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        TheBombPower power = (await PowerCmd.Apply<TheBombPower>(
            CombatState!, Owner.Creature, 3m, Owner.Creature, this))!;
        power.SetDamage(_damage);
    }
}
