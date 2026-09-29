using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves.Intents;

namespace Sts2Sim.Core.Models.Cards;

public sealed class GoForTheEyes : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 3m;
    private decimal _weak = 1m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }

    protected override async Task OnPlay(CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await DamageCmd.Attack(_damage).FromCard(this, play).Targeting(play.Target).Execute();
        if (play.Target.Monster?.NextMove?.Intents.Any(intent =>
                intent.IntentType is IntentType.Attack or IntentType.DeathBlow) == true)
            await PowerCmd.Apply<WeakPower>(CombatState!, play.Target, _weak, Owner.Creature, this);
    }

    protected override void OnUpgrade() { _damage += 1m; _weak += 1m; }
}
