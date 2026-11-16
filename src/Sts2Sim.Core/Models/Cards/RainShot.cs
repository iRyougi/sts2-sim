using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class RainShot : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal Damage => IsUpgraded ? 15m : 12m;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shoot];

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (cardPlay.Target.IsDead)
            return;

        ArrowSlot? arrow = await ArrowCmd.TryShootArrow(Owner, this);
        if (arrow is null)
            return;

        await using AttackContext context = await AttackCommand.CreateContextAsync(CombatState!, cardPlay);
        await ArrowCmd.ImpaleTarget(cardPlay.Target, arrow, async () =>
        {
            IReadOnlyList<DamageResult> results = await CreatureCmd.Damage(
                CombatState!,
                [cardPlay.Target],
                Damage,
                ValueProp.Move | ValueProp.FromArrow,
                Owner.Creature,
                this,
                cardPlay);
            context.AddHit(results);
            if (Owner.Creature.GetPower<AimPower>() is { } aim)
                await PowerCmd.Remove(aim);
            if (Owner.Creature.GetPower<QuietusPower>() is { } quietus)
                await PowerCmd.Remove(quietus);
            DamageResult? first = results.FirstOrDefault();
            if (first is not null)
            {
                Creature[] others = CombatState!.GetCreaturesOnSide(first.Receiver.Side)
                    .Where(e => !ReferenceEquals(e, cardPlay.Target) && e.IsHittable)
                    .ToArray();
                if (others.Length > 0)
                {
                    IEnumerable<DamageResult> splash = await CreatureCmd.Damage(
                        CombatState!,
                        others,
                        first.TotalDamage + first.OverkillDamage,
                        ValueProp.Move,
                        Owner.Creature,
                        this,
                        cardPlay,
                        ModifyDamageHookType.Cap);
                    context.AddHit(splash);
                }
            }
        }, this);
    }
}
