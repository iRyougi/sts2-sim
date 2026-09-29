namespace Sts2Sim.Core.Models.Afflictions;

using Sts2Sim.Core.Entities.Cards;

public sealed class Tainted : AfflictionModel
{
    public override bool IsStackable => true;
    public override bool CanAfflictCardType(CardType cardType) => cardType == CardType.Skill;
}
