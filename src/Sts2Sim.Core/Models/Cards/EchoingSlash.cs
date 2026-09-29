using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Echoing Slash: repeat the all-enemy hit once per kill.</summary>
public sealed class EchoingSlash : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 10m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await using AttackContext context = await AttackCommand.CreateContextAsync(CombatState!, cardPlay);
        int attacks = 1;
        while (attacks-- > 0)
        {
            IReadOnlyList<DamageResult> results = await CreatureCmd.Damage(
                CombatState!, CombatState!.HittableEnemies, _damage, ValueProp.Move,
                Owner.Creature, this, cardPlay);
            context.AddHit(results);
            attacks += results.Count(result => result.WasTargetKilled);
        }
    }
    protected override void OnUpgrade() => _damage += 3m;
}
