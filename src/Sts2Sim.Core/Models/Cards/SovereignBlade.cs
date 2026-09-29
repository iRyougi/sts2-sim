using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>熔炼生成的 Token 卡；Seeking Edge 将攻击改为群体，Parry 在打出时提供格挡。</summary>
public sealed class SovereignBlade : CardModel, ICardDamageVariableProvider
{
    public override bool GainsBlock => Owner is not null && Owner.Creature.Powers.OfType<ParryPower>().Any(power => power.Amount > 0m);

    private decimal _damage = 10m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        // AddDamage mutates the native DamageVar's current base value.
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 2;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain };

    public void AddDamage(decimal amount)
    {
        AssertMutable();
        _damage += amount;
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        bool targetsAllEnemies = Owner.Creature.Powers.Any(power => power is SeekingEdgePower);
        var attack = DamageCmd.Attack(_damage).FromCard(this, cardPlay);
        if (targetsAllEnemies)
        {
            await attack.TargetingAllOpponents(CombatState!).Execute();
        }
        else
        {
            ArgumentNullException.ThrowIfNull(cardPlay.Target);
            await attack.Targeting(cardPlay.Target).Execute();
        }

        decimal parryBlock = Owner.Creature.Powers
            .OfType<ParryPower>()
            .Sum(power => power.Amount);
        if (parryBlock > 0m)
        {
            await CreatureCmd.GainBlock(
                CombatState!,
                Owner.Creature,
                parryBlock,
                ValueProp.Move,
                this,
                cardPlay);
        }
    }
}
