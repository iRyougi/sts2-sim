using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PaelsLegion : RelicModel
{
    // Deviation #216 is presentation-only: skin, status display, and pet animations are omitted.
    // The pet creature, summon timing, block multiplier, and cooldown follow native behavior.
    private int _cooldown;
    private CardPlay? _affectedCardPlay;
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override bool AddsPet => true;


    public override async Task AfterObtained()
    {
        if (Owner.Creature.CombatState?.IsLiveCombat() == true)
        {
            await SummonPet();
        }
    }

    public override Task BeforeCombatStart() => SummonPet();
    public override decimal ModifyBlockMultiplicative(
        Creature target, decimal amount, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!props.HasFlag(ValueProp.Move) || cardSource?.Owner != Owner || _cooldown > 0)
            return 1m;

        return 2m;
    }

    public override Task AfterModifyingBlockAmount(
        decimal modifiedAmount,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (modifiedAmount > 0m && cardPlay is not null &&
            (_affectedCardPlay is null || ReferenceEquals(_affectedCardPlay, cardPlay)))
        {
            _affectedCardPlay = cardPlay;
        }
        return Task.CompletedTask;
    }
    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(_affectedCardPlay, cardPlay))
        {
            _affectedCardPlay = null;
            _cooldown = 2;
        }
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _cooldown--;
        }
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        _cooldown = 0;
        _affectedCardPlay = null;
        return Task.CompletedTask;
    }

    private async Task SummonPet()
    {
        await PlayerCmd.AddPet<global::Sts2Sim.Core.Models.Monsters.PaelsLegion>(Owner);
    }

    internal override void RestoreCombatCloneReferencesFrom(
        RelicModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        CardPlay? sourcePlay = ((PaelsLegion)source)._affectedCardPlay;
        if (sourcePlay is null)
        {
            _affectedCardPlay = null;
            return;
        }
        if (!cardMap.TryGetValue(sourcePlay.Card, out CardModel? mappedCard))
        {
            throw new InvalidOperationException(
                "PaelsLegion cannot clone a pending card play absent from the combat card graph.");
        }

        _affectedCardPlay = new CardPlay
        {
            Card = mappedCard,
            Player = mappedCard.Owner,
            Target = sourcePlay.Target,
            ResultPile = sourcePlay.ResultPile,
            Resources = sourcePlay.Resources,
            IsAutoPlay = sourcePlay.IsAutoPlay,
            PlayIndex = sourcePlay.PlayIndex,
            PlayCount = sourcePlay.PlayCount,
        };
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_cooldown);
        builder.Append(_affectedCardPlay is not null);
        if (_affectedCardPlay is not { } play)
            return;

        context.AppendCardReferences(ref builder, [play.Card]);
        int playerIndex = context.State.Players
            .Select((player, index) => (player, index))
            .Where(entry => ReferenceEquals(entry.player, play.Player))
            .Select(entry => entry.index)
            .DefaultIfEmpty(-1)
            .Single();
        builder.Append(playerIndex);
        builder.Append(play.Target?.CombatId);
        builder.Append((int)play.ResultPile);
        builder.Append(play.Resources.EnergySpent);
        builder.Append(play.Resources.EnergyValue);
        builder.Append(play.Resources.StarsSpent);
        builder.Append(play.Resources.StarValue);
        builder.Append(play.IsAutoPlay);
        builder.Append(play.PlayIndex);
        builder.Append(play.PlayCount);
    }
}