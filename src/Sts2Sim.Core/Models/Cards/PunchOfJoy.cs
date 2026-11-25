using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>造成 14（升级 16）点伤害，并把手中随机 1（升级 2）张 Sadness 一并变形为 Joy（v0.114.0 原版 <c>PunchOfJoy</c>）。</summary>
public sealed class PunchOfJoy : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    private decimal Damage => IsUpgraded ? 16m : 14m;
    private int Cards => IsUpgraded ? 2 : 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Cards: Cards);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        List<Sadness> sadnessInHand = Owner.PlayerCombatState!.Hand.Cards.OfType<Sadness>().ToList();
        if (sadnessInHand.Count == 0)
        {
            return;
        }

        sadnessInHand.StableShuffle(Owner.RunState.Rng.CombatCardSelection);
        var transformations = new List<CardTransformation>();
        foreach (Sadness sadness in sadnessInHand.Take(Cards))
        {
            var joy = (CardModel)ModelDb.Card<Joy>().MutableClone();
            joy.AssignOwner(Owner);
            transformations.Add(new CardTransformation(sadness, joy));
        }
        await CardCmd.Transform(transformations, null);
    }
}
