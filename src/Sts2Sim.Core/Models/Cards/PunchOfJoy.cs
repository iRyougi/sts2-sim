using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>造成 13（升级 17）点伤害，并将手中随机 1 张 Sadness 变形为 Joy（原版 <c>PunchOfJoy</c>）。</summary>
public sealed class PunchOfJoy : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    private decimal Damage => IsUpgraded ? 17m : 13m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        List<Sadness> sadnessInHand = Owner.PlayerCombatState!.Hand.Cards.OfType<Sadness>().ToList();
        if (sadnessInHand.Count > 0)
        {
            sadnessInHand.StableShuffle(Owner.RunState.Rng.CombatCardSelection);
            var joy = (CardModel)ModelDb.Card<Joy>().MutableClone();
            joy.AssignOwner(Owner);
            await CardCmd.Transform(sadnessInHand[0], joy);
        }
    }
}
