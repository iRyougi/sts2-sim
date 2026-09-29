using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Orbs;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Hibernate : CardModel, ICardChoiceBaseValueProvider
{
    private int _frostCount = 2;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay play)
    {
        await PowerCmd.Apply<HibernatePower>(CombatState!, Owner.Creature, 1m, Owner.Creature, play.Card);
        for (int i = 0; i < _frostCount; i++)
            await OrbCmd.Channel<FrostOrb>(CombatState!, Owner);
    }

    protected override void OnUpgrade() => _frostCount += 1;
}
