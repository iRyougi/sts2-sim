using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>EndOfDays</c>：3 费技能，给每个可命中的敌人施加 29（升级 37）灾厄，随后立即结算一次灾厄击杀
/// （重新读取可命中敌人里当前生命不超过灾厄层数的）。</summary>
public sealed class EndOfDays : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AllEnemies;

    protected override int CanonicalEnergyCost => 3;

    private decimal Doom => IsUpgraded ? 37m : 29m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        foreach (var enemy in CombatState!.HittableEnemies)
            await PowerCmd.Apply<DoomPower>(CombatState, enemy, Doom, Owner.Creature, this);
        await DoomPower.DoomKill(DoomPower.GetDoomedCreatures(CombatState!.HittableEnemies));
    }
}
