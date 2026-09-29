using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>亡灵契约师生成的 Token：0 费、消耗、抽 2（升级 3）。</summary>
public sealed class Soul : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    private int Cards => IsUpgraded ? 3 : 2;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    /// <summary>原版 <c>Soul.Create</c>：只建牌、指定主人，不入堆；调用方决定放到哪个牌堆。</summary>
    public static List<Soul> Create(Player owner, int amount)
    {
        var souls = new List<Soul>();
        for (int index = 0; index < amount; index++)
        {
            var soul = (Soul)ModelDb.Card<Soul>().MutableClone();
            soul.AssignOwner(owner);
            souls.Add(soul);
        }

        return souls;
    }

    public static async Task<IReadOnlyList<Soul>> CreateInHand(
        Player owner,
        int amount,
        ICombatState combatState,
        Player? creator = null)
    {
        List<Soul> souls = Create(owner, amount);
        foreach (Soul soul in souls)
        {
            await CardPileCmd.Generate(combatState, soul, PileType.Hand, creator ?? owner);
        }

        return souls;
    }

    protected override Task OnPlay(CardPlay cardPlay) =>
        CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
}
