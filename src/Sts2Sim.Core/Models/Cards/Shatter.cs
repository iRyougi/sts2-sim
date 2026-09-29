using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Shatter : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 7m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AllEnemies;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).TargetingAllOpponents(CombatState!).Execute();
        int orbCount = Owner.PlayerCombatState!.OrbQueue.Orbs.Count;
        for (int i = 0; i < orbCount; i++)
        {
            await OrbCmd.EvokeNext(CombatState!, Owner, dequeue: false);
            await OrbCmd.EvokeNext(CombatState!, Owner);
        }
    }

    protected override void OnUpgrade() => _damage += 4m;
}
