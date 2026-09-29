using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Enchantments;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BladeOfInk : CardModel
{
    private int _cards = 2;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        foreach (CardModel shiv in await Shiv.CreateInHand(Owner, _cards, CombatState!))
        {
            await CardCmd.Enchant<Inky>(shiv, 1m);
        }
    }
    protected override void OnUpgrade() => _cards++;
}
