using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class ImitationLearning : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _amount = 2m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyAlly;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        Player target = play.Target.Player
            ?? throw new InvalidOperationException("Imitation Learning target must be a player.");
        ImitationLearningPower? existing = Owner.Creature.Powers
            .OfType<ImitationLearningPower>().FirstOrDefault(power => power.PlayerTarget == target);
        if (existing is not null)
        {
            await PowerCmd.ModifyAmount(CombatState!, existing, _amount, Owner.Creature, this);
            return;
        }
        ImitationLearningPower? applied = await PowerCmd.Apply<ImitationLearningPower>(
            CombatState!, Owner.Creature, _amount, Owner.Creature, this);
        if (applied is not null) applied.PlayerTarget = target;
    }

    protected override void OnUpgrade() => _amount += 1m;
}
