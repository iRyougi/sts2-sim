using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class MasterPlannerPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner.Player && cardPlay.Card.Type == CardType.Skill)
        {
            cardPlay.Card.AddKeywordInternal(CardKeyword.Sly);
        }

        return Task.CompletedTask;
    }
}
