using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>获得 Aim 并施加绑定本卡实例的 ShowOffPower；本回合敌方致命死亡时永久提升本牌与牌组原件 Aim（原版 <c>ShowOff</c>）。</summary>
public sealed class ShowOff : CardModel, ICardChoiceBaseValueProvider, ICardComparisonBaseValueSource
{
    private const int BaseAim = 5;

    private int _currentAim = BaseAim;
    private int _increasedAim;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public int CurrentAim => _currentAim;
    public int IncreasedAim => _increasedAim;
    private decimal ShowOffPowerAmount => IsUpgraded ? 3m : 2m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public decimal GetComparisonBaseValue(string key) => key == "AimPower"
        ? _currentAim
        : throw new ArgumentOutOfRangeException(nameof(key), key, "Not a runtime-mutable comparison key.");

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<AimPower>(CombatState!, Owner.Creature, _currentAim, Owner.Creature, this);
        ShowOffPower? power = await PowerCmd.Apply<ShowOffPower>(
            CombatState!, Owner.Creature, ShowOffPowerAmount, Owner.Creature, this);
        if (power is not null)
        {
            power.ShowOffInstance = this;
        }
    }

    public void IncreaseAim(int amount)
    {
        AssertMutable();
        _increasedAim = checked(_increasedAim + amount);
        (DeckVersion as ShowOff)?.IncreaseAim(amount);
        _currentAim = checked(BaseAim + _increasedAim);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_currentAim);
        builder.Append(_increasedAim);
    }
}
