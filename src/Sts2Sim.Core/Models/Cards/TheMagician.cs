using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>增加 3（升级 4）个充能球栏位并生成对应数量随机充能球（原版 <c>TheMagician</c>）。</summary>
public sealed class TheMagician : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int Repeat => IsUpgraded ? 4 : 3;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await OrbCmd.AddSlots(Owner, Repeat);
        for (int i = 0; i < Repeat; i++)
        {
            await OrbCmd.Channel(
                CombatState!,
                OrbModel.GetRandomOrb(Owner.RunState.Rng.CombatOrbGeneration).ToMutable(),
                Owner);
        }
    }
}
