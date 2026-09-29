using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>LegionOfBone</c>（仅限多人）：每名存活玩家（含自己）按顺序各召唤 6（升级 8）。</summary>
public sealed class LegionOfBone : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AllAllies;

    public override bool IsMultiplayerOnly => true;

    protected override int CanonicalEnergyCost => 2;

    private decimal Summon => IsUpgraded ? 8m : 6m;

    // Summon 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        List<Creature> players = CombatState!.PlayerCreatures.Where(creature => creature?.IsAlive ?? false).ToList();
        foreach (Creature player in players)
            await OstyCmd.Summon(player.Player!, Summon, this);
    }
}
