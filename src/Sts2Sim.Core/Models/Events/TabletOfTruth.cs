using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 five-page decipher event. 偏离 #82/#148：省略致死提示 UI、VFX 与 DynamicVars 容器；
/// 偏离 #126：缓存成本致死分支复用 run-level LoseHp 防死/死亡 lifecycle。</summary>
public sealed class TabletOfTruth : EventModel
{
    private int _decipherCount;
    private decimal _currentCost = 3m;

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("DECIPHER", DecipherAsync),
        new EventOption("SMASH", SmashAsync),
    };

    private async Task SmashAsync()
    {
        await CreatureCmd.Heal(Owner.Creature, 20m);
        Finish();
    }

    private async Task DecipherAsync()
    {
        await LoseMaxHpAndUpgradeAsync(_currentCost);
        _decipherCount++;
        if (_decipherCount == 5)
        {
            Finish();
            return;
        }

        _currentCost = GetNextDecipherCost();
        SetOptions(new[]
        {
            new EventOption("DECIPHER", DecipherAsync),
            new EventOption("GIVE_UP", GiveUpAsync),
        });
    }

    private Task GiveUpAsync()
    {
        Finish();
        return Task.CompletedTask;
    }

    private decimal GetNextDecipherCost() => _decipherCount switch
    {
        1 => 6m,
        2 => 12m,
        3 => 24m,
        _ => Owner.Creature.MaxHp - 1m,
    };

    private async Task LoseMaxHpAndUpgradeAsync(decimal hpLoss)
    {
        if (hpLoss >= Owner.Creature.MaxHp)
        {
            await CreatureCmd.LoseMaxHp(
                RunState,
                Owner.Creature,
                Owner.Creature.MaxHp - 1m,
                isFromCard: false);
            await CreatureCmd.LoseHp(
                RunState,
                Owner.Creature,
                Owner.Creature.CurrentHp,
                ValueProp.Unblockable | ValueProp.Unpowered);
            return;
        }

        await CreatureCmd.LoseMaxHp(RunState, Owner.Creature, hpLoss, isFromCard: false);
        List<CardModel> upgradable = Owner.Deck.Cards.Where(card => card.IsUpgradable).ToList();
        if (_decipherCount == 4)
        {
            foreach (CardModel card in upgradable)
            {
                CardCmd.Upgrade(card);
            }
        }
        else if (upgradable.Count > 0)
        {
            CardModel selected = Rng.NextItem(upgradable)
                ?? throw new InvalidOperationException("No upgradable card was available.");
            CardCmd.Upgrade(selected);
        }
    }
}
