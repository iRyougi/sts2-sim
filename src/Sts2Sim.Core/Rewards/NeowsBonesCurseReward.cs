using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rewards;

/// <summary>
/// Applies Neow's Bones' mandatory curse after both relic rewards and their nested offers resolve.
/// </summary>
internal sealed class NeowsBonesCurseReward : TakeableReward
{
    public NeowsBonesCurseReward(Player player) : base(player)
    {
    }

    public override void Populate(IRunState runState)
    {
    }

    protected override async Task OnTake()
    {
        using IDisposable rngScope = Player.PlayerRng.BeginSemanticScope(
            $"{Player.CurrentSemanticLocationKey}/noncombat/NeowsBones/curse");
        // 逐条对照上游 NeowsBones.AfterObtained：候选来自 CurseCardPool（18 张显式诅咒），
        // 过滤 CanBeGeneratedByModifiers，并且 **orderby c.Id** —— 排序决定 NonCombatGeneration 抽取的索引映射，
        // 不排序会抽到不同的诅咒。此前用全体 Curse 稀有度卡的扁平集且依赖 ModelDb 顺序（偏离 #301）。
        List<CardModel> curses = CurseCardPool.Instance
            .GetUnlockedCards(Player.UnlockState, isMultiplayer: Player.RunState.Players.Count > 1)
            .Where(card => card.CanBeGeneratedByModifiers)
            .OrderBy(card => card.Id)
            .ToList();
        CardModel canonical = Player.PlayerRng.NonCombatGeneration.NextItem(curses)
            ?? throw new InvalidOperationException("No eligible curse is available for NeowsBones.");
        var curse = (CardModel)canonical.MutableClone();
        curse.AssignOwner(Player);
        await CardPileCmd.AddToDeck(curse);
    }
}
