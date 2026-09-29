using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Ignition : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyAlly;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        return OrbCmd.Channel<PlasmaOrb>(CombatState!, play.Target.Player
            ?? throw new InvalidOperationException("Ignition target must be a player."));
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
