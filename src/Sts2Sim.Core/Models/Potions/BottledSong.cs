using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Potions;

/// <summary>生成 Wah、Cho、Ki 旋律牌各 1 张入手牌并本回合免费（原版 <c>BottledSong</c>）。</summary>
public sealed class BottledSong : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Player targetPlayer = target.Player!;
        CardTag[] melodyTags = [CardTag.Wah, CardTag.Cho, CardTag.Ki];
        bool isMultiplayer = targetPlayer.RunState.Players.Count > 1;
        CardModel[] melodyCards = targetPlayer.UnlockState.CharacterCardPools
            .SelectMany(pool => pool.GetUnlockedCards(targetPlayer.UnlockState, isMultiplayer))
            .Where(card => card.Tags.Contains(CardTag.Wah) || card.Tags.Contains(CardTag.Cho) || card.Tags.Contains(CardTag.Ki))
            .ToArray();

        var generated = new List<CardModel>();
        foreach (CardTag melodyTag in melodyTags)
        {
            CardModel? picked = CardFactory.GetDistinctForCombat(
                targetPlayer,
                melodyCards.Where(card => card.Tags.Contains(melodyTag)),
                1,
                targetPlayer.RunState.Rng.CombatCardGeneration).FirstOrDefault();
            if (picked is not null)
            {
                picked.SetToFreeThisTurn();
                generated.Add(picked);
            }
        }

        foreach (CardModel card in generated)
        {
            await CardPileCmd.Generate(target.CombatState!, card, PileType.Hand, Owner);
        }
    }
}
