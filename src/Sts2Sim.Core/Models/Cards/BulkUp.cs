using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BulkUp : CardModel, ICardChoiceBaseValueProvider
{
    private int _orbSlots = 1;
    private decimal _strength = 2m;
    private decimal _dexterity = 2m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        OrbCmd.RemoveSlots(Owner, _orbSlots);
        await PowerCmd.Apply<StrengthPower>(CombatState!, Owner.Creature, _strength, Owner.Creature, this);
        await PowerCmd.Apply<DexterityPower>(CombatState!, Owner.Creature, _dexterity, Owner.Creature, this);
    }
    protected override void OnUpgrade() { _strength += 1m; _dexterity += 1m; }
}
