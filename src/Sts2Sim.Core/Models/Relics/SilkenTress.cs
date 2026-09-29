using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SilkenTress : RelicModel
{
    private bool _isUsed;

    public override RelicRarity Rarity => RelicRarity.Ancient;

    public override bool IsUsedUp => _isUsed;

    public override Task AfterObtained() =>
        PlayerCmd.LoseGold(Owner.Gold, Owner, GoldLossType.Stolen);

    public override CardModel? TryModifyCardRewardOptionLate(
        IRunState runState, Player player, CardModel option, CardCreationOptions creationOptions) =>
        creationOptions.Flags.HasFlag(CardCreationFlags.IsCardReward)
            ? TryModifyCardRewardOptionLate(runState, player, option) : null;

    public override CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option)
    {
        if (_isUsed || !ReferenceEquals(player, Owner))
        {
            return null;
        }

        var glam = (Glam)ModelDb.Get(typeof(Glam));
        if (!glam.CanEnchant(option))
        {
            return option;
        }

        var clone = (CardModel)option.MutableClone();
        // 偏离 #153：late reward hook 为同步签名，只能同步等待异步 Enchant 命令。
        CardCmd.Enchant<Glam>(clone, 1m).GetAwaiter().GetResult();
        return clone;
    }

    public override void AfterModifyingCardRewardOptions(
        IRunState runState,
        Player player,
        IReadOnlyList<CardModel> options)
    {
        if (!_isUsed && ReferenceEquals(player, Owner))
        {
            _isUsed = true;
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_isUsed);
    }
}
