using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>GlimpseBeyond</c>（仅限多人）：消耗；己方每名存活玩家（含自己）各把 3（升级 4）张
/// <see cref="Soul"/> 洗进自己抽牌堆的随机位置，生成者为本牌持有者。</summary>
public sealed class GlimpseBeyond : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AllAllies;

    public override bool IsMultiplayerOnly => true;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    private int Cards => IsUpgraded ? 4 : 3;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        // 原版 GetTeammatesOf = 同一方全部生物（含自己），再筛存活玩家；列表在循环前取定。
        List<Creature> teammates = CombatState!.GetCreaturesOnSide(Owner.Creature.Side)
            .Where(creature => creature is not null && creature.IsAlive && creature.IsPlayer)
            .ToList();
        foreach (Creature teammate in teammates)
        {
            var souls = new List<Soul>(Cards);
            for (int i = 0; i < Cards; i++)
            {
                var soul = (Soul)ModelDb.Card<Soul>().MutableClone();
                soul.AssignOwner(teammate.Player!);
                souls.Add(soul);
            }

            foreach (Soul soul in souls)
                await CardPileCmd.Generate(CombatState!, soul, PileType.Draw, Owner, CardPilePosition.Random);
        }
    }
}
