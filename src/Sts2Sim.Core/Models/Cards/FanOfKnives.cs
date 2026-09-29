using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class FanOfKnives : CardModel
{
    private int _cards = 4;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<FanOfKnivesPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
        await Shiv.CreateInHand(Owner, _cards, CombatState!);
    }
    protected override void OnUpgrade() => _cards++;
}
