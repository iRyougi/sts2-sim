using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>v0.113.1 <c>MegaCrit.Sts2.Core.Models.Cards.GodByrd</c>：Byrdine 进化后由 ByrdSwoop 转化而来；
/// 打出时给自己施加等于伤害值的 <see cref="GodByrdPower"/>（30，升级 +10，Unpowered）。</summary>
public sealed class GodByrd : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _damage = 30m;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.None;
    protected override int CanonicalEnergyCost => 0;

    public decimal Damage => _damage;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<GodByrdPower>(Owner.Creature.CombatState!, Owner.Creature, _damage, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _damage += 10m;
}
