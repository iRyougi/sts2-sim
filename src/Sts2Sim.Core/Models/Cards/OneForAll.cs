using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class OneForAll : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _amount = 3m;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AllAllies;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay play)
    {
        foreach (Player player in CombatState?.Players ?? [])
            await PowerCmd.Apply<OneForAllPower>(CombatState!, player.Creature,
                _amount, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _amount += 1m;
}
