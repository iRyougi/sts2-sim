using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.PotionPools;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

/// <summary>v0.113.1 Underdocks 宠物事件 <c>WuppyInTrouble</c>（#928）。
/// 立刻帮助 = Injury + 60 最大 HP 的 Wuppy；谨慎 = 失去当前 HP 的 5%（Unblockable|Unpowered）+ 30 最大 HP 的 Wuppy；
/// 忽略 = 从角色药水池 + 共享药水池用 PlayerRng.Rewards 抽一瓶药水作为奖励。任一玩家已有事件宠物或
/// 当前 HP 不高于损失值时不出现。忽略分支抽到的具体药水在选择前不可见。</summary>
public sealed class WuppyInTrouble : EventModel
{
    public const int ImmediateWuppyHp = 60;
    public const int CautiousWuppyHp = 30;

    private decimal _hpLoss;

    public decimal HpLoss => _hpLoss;

    protected override VisibleDecisionMetadata BuildVisibleState() => VisibleDecisionMetadata.Empty;

    protected override VisibleOption DescribeVisibleOption(EventOption option, int displayIndex, VisibleOption declared) =>
        option.Key switch
        {
            "HELP_IMMEDIATELY" => new(option.Key, [
                VisibleValue.Id("Relic", ModelDb.GetId<Wuppy>().Entry), VisibleValue.Int("WuppyMaxHp", ImmediateWuppyHp),
                VisibleValue.Id("Curse", ModelDb.GetId<Injury>().Entry), VisibleValue.Int("CurseCount", 1)])
                { Contents = [VisibleContentProjection.ForRelic(ModelDb.Relic<Wuppy>()),
                    VisibleContentProjection.ForCard(ModelDb.Card<Injury>())] },
            "BE_CAUTIOUS" => new(option.Key, [
                VisibleValue.Id("Relic", ModelDb.GetId<Wuppy>().Entry), VisibleValue.Int("WuppyMaxHp", CautiousWuppyHp),
                VisibleValue.Number("HpLoss", _hpLoss)])
                { Contents = [VisibleContentProjection.ForRelic(ModelDb.Relic<Wuppy>())] },
            "IGNORE" => new(option.Key, [VisibleValue.Scope("RewardKind", "Potion"), VisibleValue.Int("Count", 1),
                VisibleValue.Condition("Targets", "Random")]),
            _ => base.DescribeVisibleOption(option, displayIndex, declared),
        };

    public override bool IsAllowed(IRunState runState)
    {
        foreach (Player player in runState.Players)
        {
            if (player.Creature.CurrentHp <= GetHpLoss(player)) return false;
            if (player.HasEventPet()) return false;
        }
        return true;
    }

    private static decimal GetHpLoss(Player player) => player.Creature.CurrentHp * 0.05m;

    protected override void CalculateVars()
    {
        AssertMutable();
        _hpLoss = GetHpLoss(Owner);
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        new EventOption("HELP_IMMEDIATELY", HelpImmediately),
        new EventOption("BE_CAUTIOUS", BeCautious),
        new EventOption("IGNORE", Ignore),
    ];

    private async Task HelpImmediately()
    {
        var injury = (CardModel)ModelDb.Card<Injury>().MutableClone();
        injury.AssignOwner(Owner);
        await CardPileCmd.AddToDeck(injury);
        await ObtainWuppy(ImmediateWuppyHp);
        Finish();
    }

    private async Task BeCautious()
    {
        await CreatureCmd.Damage(RunState, Owner.Creature, _hpLoss, ValueProp.Unblockable | ValueProp.Unpowered);
        await ObtainWuppy(CautiousWuppyHp);
        Finish();
    }

    private async Task ObtainWuppy(int maxHp)
    {
        var wuppy = (Wuppy)ModelDb.Relic<Wuppy>().MutableClone();
        wuppy.MaxHp = maxHp;
        await RelicCmd.Obtain(wuppy, Owner);
    }

    private Task Ignore()
    {
        IEnumerable<PotionModel> potions = Owner.Character.PotionPool.GetUnlockedPotions(Owner.UnlockState)
            .Concat(SharedPotionPool.Instance.GetUnlockedPotions(Owner.UnlockState));
        PotionModel? potion = Owner.PlayerRng.Rewards.NextItem(potions);
        if (potion is not null)
            OfferRewards(RewardsSet.CreateCustom(Owner,
                potion: new PotionReward((PotionModel)potion.MutableClone(), Owner)));
        Finish();
        return Task.CompletedTask;
    }
}
