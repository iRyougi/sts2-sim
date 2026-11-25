using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Quiver : CardModel, ICardChoiceBaseValueProvider
{
    private const int Repeat = 1;

    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay)
    {
        for (int i = 0; i < Repeat; i++)
        {
            ArrowCmd.GainArrowSlot(Owner);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);
}
