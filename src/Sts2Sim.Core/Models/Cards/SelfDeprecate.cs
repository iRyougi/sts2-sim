using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>仅多人牌：向自己生成 1 张 Sadness 并向其他队友生成 Joy（原版 <c>SelfDeprecate</c>）。</summary>
public sealed class SelfDeprecate : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AllAllies;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 1;
    private int JoyCount => IsUpgraded ? 3 : 2;
    private const int SadnessCount = 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        for (int i = 0; i < SadnessCount; i++)
        {
            var sadness = (CardModel)ModelDb.Card<Sadness>().MutableClone();
            sadness.AssignOwner(Owner);
            await CardPileCmd.Generate(CombatState!, sadness, PileType.Hand, Owner);
        }

        if (CombatState is null)
            return;

        Creature[] teammates = CombatState.GetCreaturesOnSide(Owner.Creature.Side)
            .Where(c => c is not null && c.IsAlive && c.IsPlayer && !ReferenceEquals(c.Player, Owner))
            .ToArray();
        foreach (Creature teammate in teammates)
        {
            for (int i = 0; i < JoyCount; i++)
            {
                var joy = (CardModel)ModelDb.Card<Joy>().MutableClone();
                joy.AssignOwner(teammate.Player!);
                await CardPileCmd.Generate(CombatState, joy, PileType.Hand, Owner);
            }
        }
    }
}
