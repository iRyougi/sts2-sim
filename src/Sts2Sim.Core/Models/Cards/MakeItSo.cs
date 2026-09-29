using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>6伤害;技能牌用完达3倍数时回手。逐字移植核心行为（<c>MegaCrit.Sts2.Core.Models.Cards.MakeItSo</c>），
/// 偏离 #101：用 <see cref="Entities.Players.PlayerCombatState.SkillCardsPlayedThisTurn"/> 计数器
/// 代替战斗历史日志；计数器在 <c>Hook.AfterCardPlayed</c> 分发前已记录当前完成出牌。</summary>
public sealed class MakeItSo : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 6m;
    private const int Threshold = 3;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player == Owner && cardPlay.Card.Type == CardType.Skill && Pile?.Type != PileType.Hand)
        {
            if (Owner.PlayerCombatState!.SkillCardsPlayedThisTurn % Threshold == 0)
            {
                CardPileCmd.Add(this, PileType.Hand);
            }
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => _damage += 3m;
}
