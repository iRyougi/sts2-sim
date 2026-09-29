using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Cacophony</c>：仅限多人的 2 费能力，施加层数 66（升级 99）的 <see cref="CacophonyPower"/>
/// （任何牌每被抽 33 张，对随机敌人造成层数的无力伤害）。</summary>
public sealed class Cacophony : CardModel, ICardChoiceBaseValueProvider
{
    private const int CardsPerTrigger = 33;

    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsMultiplayerOnly => true;

    protected override int CanonicalEnergyCost => 2;

    private decimal Damage => IsUpgraded ? 99m : 66m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Cards: CardsPerTrigger);

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<CacophonyPower>(CombatState!, Owner.Creature, Damage, Owner.Creature, this);
}
