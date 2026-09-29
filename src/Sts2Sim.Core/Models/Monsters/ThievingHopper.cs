namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class ThievingHopper : MonsterModel
{
    public bool IsHovering { get; private set; }
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 84, 79);
    public override int MaxInitialHp => MinInitialHp;
    private int TheftDamage => Ascension(AscensionLevel.DeadlyEnemies, 19, 17);
    private int HatTrickDamage => Ascension(AscensionLevel.DeadlyEnemies, 23, 21);
    private int NabDamage => Ascension(AscensionLevel.DeadlyEnemies, 16, 14);

    public override async Task AfterAddedToRoom() =>
        await PowerCmd.Apply<EscapeArtistPower>(Creature.CombatState!, Creature, 5m, Creature, null);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var theft = new MoveState("THIEVERY_MOVE", Theft, new SingleAttackIntent(TheftDamage), new CardDebuffIntent());
        var flutter = new MoveState("FLUTTER_MOVE", Flutter, new BuffIntent());
        var hat = new MoveState("HAT_TRICK_MOVE", _ => DamageCmd.Attack(HatTrickDamage).FromMonster(this).Execute(), new SingleAttackIntent(HatTrickDamage));
        var nab = new MoveState("NAB_MOVE", _ => DamageCmd.Attack(NabDamage).FromMonster(this).Execute(), new SingleAttackIntent(NabDamage));
        var escape = new MoveState("ESCAPE_MOVE", Escape, new EscapeIntent());
        theft.FollowUpState = flutter;
        flutter.FollowUpState = hat;
        hat.FollowUpState = nab;
        nab.FollowUpState = escape;
        escape.FollowUpState = escape;
        return new MonsterMoveStateMachine([theft, flutter, hat, nab, escape], theft);
    }

    private async Task Theft(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.Player is not null && !target.IsDead))
        {
            List<CardModel> eligible = target.Player!.PlayerCombatState!.DrawPile.Cards
                .Concat(target.Player.PlayerCombatState.DiscardPile.Cards)
                .Where(card => card.DeckVersion is not null)
                .ToList();
            if (eligible.Count == 0) continue;
            int priority = eligible.Min(Priority);
            CardModel combatCard = RunRng.CombatCardGeneration.NextItem(
                eligible.Where(card => Priority(card) == priority))!;
            CardPileCmd.Remove(combatCard);
            SwipePower swipe = await PowerCmd.Apply<SwipePower>(Creature.CombatState!, Creature, 1m, Creature, null)
                ?? throw new InvalidOperationException("Thievery must create SwipePower.");
            await swipe.Steal(combatCard);
        }
        await DamageCmd.Attack(TheftDamage).FromMonster(this).Execute();
    }

    private static int Priority(CardModel card)
    {
        if (card.Rarity == CardRarity.Ancient || card.Enchantments.Any(enchantment => enchantment is Imbued))
        {
            return 3;
        }

        return card.Rarity switch
        {
            CardRarity.Uncommon => 0,
            CardRarity.Common or CardRarity.Rare or CardRarity.Event => 1,
            CardRarity.Basic or CardRarity.Quest => 2,
            _ => 4,
        };
    }

    private async Task Flutter(IReadOnlyList<Creature> targets)
    {
        IsHovering = true;
        await PowerCmd.Apply<FlutterPower>(Creature.CombatState!, Creature, 5m, Creature, null);
    }

    internal void StopHovering()
    {
        AssertMutable();
        IsHovering = false;
    }

    private async Task Escape(IReadOnlyList<Creature> targets)
    {
        StopHovering();
        await CreatureCmd.Escape(Creature);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
