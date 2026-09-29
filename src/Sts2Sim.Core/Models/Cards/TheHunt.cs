using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Cards;

public sealed class TheHunt : CardModel, ICardDamageVariableProvider
{
    private int _pendingCardRewards;

    internal int PendingCardRewardsForCombatSearch => _pendingCardRewards;
    private decimal Damage => IsUpgraded ? 15m : 10m;
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        bool shouldTriggerFatal = play.Target.Powers.All(power => power.ShouldOwnerDeathTriggerFatal());
        AttackCommand attack = await DamageCmd.Attack(Damage).FromCard(this, play).Targeting(play.Target).Execute();
        if (shouldTriggerFatal && attack.Results.SelectMany(results => results).Any(result => result.WasTargetKilled))
        {
            // Keep the earned-resource counter local for projection cloning/fingerprinting.
            _pendingCardRewards++;
            if (CombatState is not CombatState { IsProjection: true } &&
                Owner.RunState.CurrentRoom is CombatRoom room)
            {
                CardRarityOddsType oddsType = room.RoomType switch
                {
                    RoomType.Elite => CardRarityOddsType.EliteEncounter,
                    RoomType.Boss => CardRarityOddsType.BossEncounter,
                    _ => CardRarityOddsType.RegularEncounter,
                };
                room.QueueExtraReward(Owner, new CardReward(Owner, oddsType));
            }
            await PowerCmd.Apply<TheHuntPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_pendingCardRewards);
    }

}
