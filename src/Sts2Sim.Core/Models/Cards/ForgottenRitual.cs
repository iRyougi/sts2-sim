using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class ForgottenRitual : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: _energy);

    private int _energy = 3;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    // v0.113.1：只有本回合本玩家已消耗过卡（CardExhaustedEntry.Actor == Owner.Creature）才获得能量。
    protected override Task OnPlay(CardPlay cardPlay) =>
        CombatState is CombatState concrete && concrete.CountCardsExhaustedThisTurn(Owner) > 0
            ? PlayerCmd.GainEnergy(_energy, Owner)
            : Task.CompletedTask;
    protected override void OnUpgrade() => _energy++;
}
