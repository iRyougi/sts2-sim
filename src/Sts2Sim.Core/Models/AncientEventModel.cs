using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;
using System.Collections.Immutable;

namespace Sts2Sim.Core.Models;

/// <summary>
/// Ancient event base shared by Neow and the Hive Ancients.
/// Neow resets HP before healing; WearyTraveler applies to that full missing-health amount.
/// 偏离 #144：该钩子对应 WaxChoker 对 Ancient 入口的拦截语义；默认允许，但其他监听器仍可否决。
/// </summary>
public abstract class AncientEventModel : EventModel
{
    /// <summary>本 Ancient 事件理论上可能提供的全部遗物选项（NeowsBones 读取这个属性来排除自身）。</summary>
    public abstract IReadOnlyList<RelicModel> AllPossibleOptions { get; }

    protected override VisibleDecisionMetadata BuildVisibleState()
    {
        // Only the eight audited Ancient providers have complete current-offer snapshots.
        // Future Ancient implementations must declare their own display surface.
        if (this is not (Events.Neow or Events.Darv or Events.Nonupeipe or Events.Orobas
            or Events.Pael or Events.Tanx or Events.Tezcatara or Events.Vakuu))
            return base.BuildVisibleState();

        return new([], CurrentOptions.Select(option => new VisibleOption(option.Key, [])
        {
            IsEnabled = option.IsEnabled,
            CoverageGapIds = option.OfferedContent is not null || option.Key == "PROCEED"
                || (this is Events.Orobas && option.Key == "OPTION_POOL_3_LOCKED" && option.IsLocked)
                ? [] : ["Unsupported.AncientOptionMetadata"],
        }).ToImmutableArray());
    }

    protected EventOption RelicOption<TRelic>(string key)
        where TRelic : RelicModel
        => RelicOption(typeof(TRelic), key);

    /// <summary>非泛型重载：供 <c>Neow</c> 这类需要在运行时按 <see cref="Type"/> 动态选择遗物的调用方使用。</summary>
    protected EventOption RelicOption(Type relicType, string key)
    {
        // Capture only this selected display item; the obtain callback keeps its original clone timing.
        VisibleContent offeredContent = DescribeOfferedRelic((RelicModel)ModelDb.Get(relicType));
        return new EventOption(key, async () =>
        {
            RelicModel relic = (RelicModel)ModelDb.Get(relicType).MutableClone();
            await RelicCmd.Obtain(relic, Owner);
            Finish();
        }, offeredContent);
    }

    /// <summary>Preserve an Ancient option's preconfigured relic state until the player chooses it.</summary>
    protected EventOption RelicOption(RelicModel relic, string key)
    {
        return new EventOption(key, async () =>
        {
            await RelicCmd.Obtain(relic, Owner);
            Finish();
        }, DescribeOfferedRelic(relic));
    }

    // The option owns a pure snapshot. Reading a decision never initializes model prototypes,
    // reruns setup, or evaluates an obtain callback.
    private VisibleContent DescribeOfferedRelic(RelicModel relic)
    {
        VisibleContent content = VisibleContentProjection.ForRelic(relic);
        var values = content.Values.ToBuilder();
        var children = content.Children.ToBuilder();
        switch (relic)
        {
            case DustyTome { AncientCard: { } ancientCard }:
                values.Add(VisibleValue.Id("AncientCard", ancientCard.Entry));
                children.Add(DescribeAncientCardPreview(ModelDb.GetById<CardModel>(ancientCard), 1, []));
                break;
            case SeaGlass { CharacterId: { } characterId }:
                values.Add(VisibleValue.Id("CharacterId", characterId.Entry));
                break;
            case TouchOfOrobas { StarterRelic: { } starter, UpgradedRelic: { } upgraded }:
                values.Add(VisibleValue.Id("StarterRelic", starter.Entry));
                values.Add(VisibleValue.Id("UpgradedRelic", upgraded.Entry));
                children.Add(VisibleContentProjection.ForRelic(ModelDb.GetById<RelicModel>(starter)));
                children.Add(VisibleContentProjection.ForRelic(ModelDb.GetById<RelicModel>(upgraded)));
                break;
            case ArchaicTooth:
                CardModel? original = Owner.Deck.Cards.FirstOrDefault(ArchaicTooth.IsTranscendenceStarter);
                if (original is not null && ArchaicTooth.TryGetTranscendenceUpgrade(original) is { } replacement)
                {
                    values.Add(VisibleValue.Id("StarterCard", original.Id.Entry));
                    values.Add(VisibleValue.Id("AncientCard", replacement.Id.Entry));
                    children.Add(VisibleContentProjection.ForCard(original));
                    // Native preview preserves one upgrade and the existing enchantment. Describe the
                    // deterministic result without executing Upgrade/Enchant or creating a run card.
                    children.Add(DescribeAncientCardPreview(replacement,
                        original.IsUpgraded ? 1 : 0, original.Enchantments));
                }
                break;
        }
        return content with { Values = values.ToImmutable(), Children = children.ToImmutable() };
    }

    private static VisibleContent DescribeAncientCardPreview(
        CardModel canonical, int upgradeLevel, IReadOnlyList<EnchantmentModel> enchantments)
    {
        // 41cef1ea: these are the finite DustyTome/ArchaicTooth preview endpoints.
        // Read displayed identity/cost classifications; do not execute Upgrade or Enchant.
        int upgradeCostReduction = canonical switch
        {
            Abundance or Apotheosis or Corruption or ForbiddenGrimoire or Protector or Quadcast
                or TheSealedThrone => 1,
            Apparition or BiasedCognition or BrightestFlame or Maul or NeowsFury or Relax
                or Whistle or Wish or WraithForm or Break or Suppress or MeteorShower => 0,
            _ => throw new InvalidOperationException($"Unaudited Ancient card preview: {canonical.Id.Entry}."),
        };
        VisibleContent baseline = VisibleContentProjection.ForCard(canonical);
        var values = baseline.Values.Where(value => value.Name is
            "Rarity" or "EnergyCost" or "StarCost").ToImmutableArray().ToBuilder();
        bool hasFreeEnchantment = enchantments.Any(e => e is TezcatarasEmber);
        if ((upgradeLevel > 0 && upgradeCostReduction > 0) || hasFreeEnchantment)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Name != "EnergyCost") continue;
                int cost = hasFreeEnchantment
                    ? 0 : Math.Max(0, canonical.CanonicalEnergyCostValue - upgradeCostReduction);
                values[i] = VisibleValue.Int("EnergyCost", cost);
            }
        }
        values.Add(VisibleValue.Int("UpgradeLevel", upgradeLevel));
        values.Add(VisibleValue.Scope("Role", "AncientCard"));
        return new(canonical.Id.Entry, values.ToImmutable(),
            enchantments.Select(VisibleContentProjection.ForEnchantment).ToImmutableArray())
            { CoverageGapIds = ["G2.Canonical"] };
    }

    /// <summary>
    /// 偏离 #144：监听器否决时，仅提供用于完成事件的 PROCEED 选项。
    /// </summary>
    protected sealed override IReadOnlyList<EventOption> GenerateInitialOptionsWrapper()
    {
        if (Hook.ShouldAllowAncient(RunState, Owner, this))
        {
            return base.GenerateInitialOptionsWrapper();
        }

        return new[] { new EventOption("PROCEED", () => { Finish(); return Task.CompletedTask; }) };
    }

    /// <summary>
    /// 偏离 #145：<see cref="EventModel.BeginEvent"/> 目前是同步 API，必须在此同步等待异步治疗命令；
    /// 若将来 BeginEvent 改为异步签名，应同步简化本实现。
    /// </summary>
    protected override void CalculateVars()
    {
        base.CalculateVars();
        // Authoritative BeforeEventStarted resets Neow HP even without the UI animation.
        // At A10 this produces the archived initial heal of 56, rather than retaining 70 HP.
        if (this is Events.Neow)
        {
            Owner.Creature.SetCurrentHpInternal(0m);
        }
        decimal missingHp = Owner.Creature.MaxHp - Owner.Creature.CurrentHp;
        decimal heal = RunState.Ascension.HasLevel(AscensionLevel.WearyTraveler) ? missingHp * 0.8m : missingHp;
        CreatureCmd.Heal(Owner.Creature, heal).GetAwaiter().GetResult();
    }
}
