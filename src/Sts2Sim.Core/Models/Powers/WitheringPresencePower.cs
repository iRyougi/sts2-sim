namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;

public sealed class WitheringPresencePower : PowerModel
{
    private int _cardsPlayed;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public Creature Target { get; set; } = null!;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Player.Creature, Target))
        {
            return;
        }

        _cardsPlayed++;
        if (_cardsPlayed < Amount)
        {
            return;
        }

        _cardsPlayed = 0;
        var wither = (Wither)ModelDb.Card<Wither>().MutableClone();
        wither.AssignOwner(Target.Player!);
        await CardPileCmd.Generate(Owner.CombatState!, wither, PileType.Hand, creator: null);
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        Target = creatureMap[((WitheringPresencePower)source).Target];
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(Target.CombatId);
        builder.Append(_cardsPlayed);
    }
}
