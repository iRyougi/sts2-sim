using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Shiv : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType =>
        IsMutable && HasOwner && Owner.Creature.HasPower<FanOfKnivesPower>()
            ? TargetType.AllEnemies
            : TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shiv];

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    private decimal Damage => IsUpgraded ? 6m : 4m;

    public static async Task<CardModel?> CreateInHand(
        Player owner,
        ICombatState combatState,
        Player? creator = null)
    {
        foreach (CardModel shiv in await CreateInHand(owner, 1, combatState, creator))
        {
            return shiv;
        }

        return null;
    }

    public static async Task<IEnumerable<CardModel>> CreateInHand(
        Player owner,
        int count,
        ICombatState combatState,
        Player? creator = null)
    {
        var shivs = new List<CardModel>();
        for (int index = 0; index < count; index++)
        {
            var shiv = (Shiv)ModelDb.Card<Shiv>().MutableClone();
            shiv.AssignOwner(owner);
            await CardPileCmd.Generate(combatState, shiv, PileType.Hand, creator ?? owner);
            shivs.Add(shiv);
        }

        return shivs;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        var attack = DamageCmd.Attack(Damage).FromCard(this, cardPlay);
        if (TargetType == TargetType.AllEnemies)
        {
            await attack.TargetingAllOpponents(CombatState!).Execute();
            return;
        }

        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await attack.Targeting(cardPlay.Target).Execute();
    }
}
