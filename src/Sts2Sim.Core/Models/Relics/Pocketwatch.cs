using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Pocketwatch : RelicModel
{
    private const int CardPlayThreshold = 3;
    private const int ExtraDraw = 3;
    private const int PackedCountMask = 0xFFFF;

    // The high word records the count immediately before the existing turn reset. Reusing the
    // already-fingerprinted field keeps the new retention snapshot behavior-neutral and avoids
    // introducing a second mutable source of truth for combat cloning.
    private int _cardsPlayedThisTurn;
    private bool _shouldDrawExtra;

    internal int CardsPlayedThisTurn => _cardsPlayedThisTurn & PackedCountMask;
    internal int CardsPlayedPreviousTurn => (int)((uint)_cardsPlayedThisTurn >> 16);
    internal bool ShouldDrawExtra => _shouldDrawExtra;
    internal bool CanStillTriggerThisTurn => CardsPlayedThisTurn <= CardPlayThreshold;
    internal int CardPlayThresholdSnapshot => CardPlayThreshold;

    public int CardsPlayedThisTurnSnapshot => CardsPlayedThisTurn;
    public int CardsPlayedPreviousTurnSnapshot => CardsPlayedPreviousTurn;
    public bool ShouldDrawExtraSnapshot => ShouldDrawExtra;
    public bool CanStillTriggerThisTurnSnapshot => CanStillTriggerThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player == Owner)
        {
            int current = Math.Min(PackedCountMask, CardsPlayedThisTurn + 1);
            _cardsPlayedThisTurn = PackCounts(current, CardsPlayedPreviousTurn);
        }

        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _shouldDrawExtra =
                Owner.PlayerCombatState!.TurnNumber >= 1 &&
                CardsPlayedThisTurn <= CardPlayThreshold;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyHandDraw(Player player, decimal originalCardCount)
    {
        if (player != Owner || !_shouldDrawExtra)
        {
            return originalCardCount;
        }

        _shouldDrawExtra = false;
        return originalCardCount + ExtraDraw;
    }

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _cardsPlayedThisTurn = PackCounts(current: 0, previous: CardsPlayedThisTurn);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        _cardsPlayedThisTurn = 0;
        _shouldDrawExtra = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(CardsPlayedThisTurn);
        builder.Append(CardsPlayedPreviousTurn);
        builder.Append(_shouldDrawExtra);
    }

    private static int PackCounts(int current, int previous) =>
        (previous & PackedCountMask) << 16 | current & PackedCountMask;
}
