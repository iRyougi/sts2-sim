using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>收回所有SovereignBlade进手牌,熔炼8点伤害。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.SummonForth</c>）。</summary>
public sealed class SummonForth : CardModel
{
    private decimal _forge = 8m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IEnumerable<SovereignBlade> blades = Owner.PlayerCombatState!.AllPiles
            .SelectMany(pile => pile.Cards)
            .OfType<SovereignBlade>()
            .Where(blade => blade.Pile?.Type != PileType.Hand)
            .ToList();
        foreach (SovereignBlade blade in blades)
        {
            CardPileCmd.Add(blade, PileType.Hand);
        }

        await ForgeCmd.Forge(_forge, Owner, this);
    }

    protected override void OnUpgrade() => _forge += 3m;
}
