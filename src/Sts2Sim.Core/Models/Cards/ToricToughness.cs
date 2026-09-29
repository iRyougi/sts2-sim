using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class ToricToughness : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 5m;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal actualBlock = await CreatureCmd.GainBlock(
            CombatState!,
            Owner.Creature,
            _block,
            ValueProp.Move,
            this,
            cardPlay);
        ToricToughnessPower? power = await PowerCmd.Apply<ToricToughnessPower>(
            CombatState!,
            Owner.Creature,
            2m,
            Owner.Creature,
            this);
        power?.StoreBlock(actualBlock);
    }

    protected override void OnUpgrade() => _block = 7m;
}
