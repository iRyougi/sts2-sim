using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class UpMySleeve : CardModel
{
    private int _cards = 3;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await Shiv.CreateInHand(Owner, _cards, CombatState!);
        AddEnergyCostThisCombat(-1);
    }
    protected override void OnUpgrade() => _cards++;
}
