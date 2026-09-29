using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.ValueProps;
using Sts2Sim.Core.Models.CardPools;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Gain block and create an eligible colorless card. The created clone is upgraded when this card is upgraded.
/// </summary>
public sealed class ManifestAuthority : CardModel
{
    public override bool GainsBlock => true;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ICombatState combatState = CombatState ?? throw new InvalidOperationException("Manifest Authority requires combat.");
        await CreatureCmd.GainBlock(
            combatState,
            Owner.Creature,
            IsUpgraded ? 8m : 7m,
            ValueProp.Move,
            this,
            cardPlay);

        CardModel? generated = CardFactory.GetDistinctForCombat(
            Owner, ColorlessCardPool.Instance.GetUnlockedCards(
                Owner.UnlockState, Owner.RunState.Players.Count > 1), 1, combatState.RunState.Rng.CombatCardGeneration).FirstOrDefault();
        if (generated is null)
        {
            return;
        }

        if (IsUpgraded)
        {
            generated.Upgrade();
        }

        await CardPileCmd.Generate(combatState, generated, PileType.Hand);
    }
}
