using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BulletTime : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        foreach (CardModel card in Owner.PlayerCombatState!.Hand.Cards)
        {
            if (!card.CostsXEnergy)
            {
                card.MakeTemporaryFreeThisTurn();
            }
        }

        await PowerCmd.Apply<NoDrawPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
