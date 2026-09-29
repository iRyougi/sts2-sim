using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust，生成一瓶当前角色或共享池中的战斗可生成药水。
/// 与原生 Alchemize 一样，药水槽已满时仍先抽稀有度和具体药水，再由 TryToProcure 拒绝；
/// 若当前测试注册表中该稀有度无候选，则不生成药水。</summary>
public sealed class Alchemize : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    public override bool CanBeGeneratedInCombat => false;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        PotionFactory.Rarity rarity = PotionFactory.RollRarity(Owner.RunState.Rng.CombatPotionGeneration);
        Models.PotionModel? potion = PotionFactory.CreateRandomForCombat(
            Owner, rarity, Owner.RunState.Rng.CombatPotionGeneration);
        if (potion is not null)
        {
            await PotionCmd.TryToProcure(potion, Owner);
        }
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
