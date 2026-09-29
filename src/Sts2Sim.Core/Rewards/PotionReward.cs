using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Entities.Rngs;

namespace Sts2Sim.Core.Rewards;

/// <summary>One potion reward drawn from the player's reward RNG stream.</summary>
public sealed class PotionReward : TakeableReward
{
    public PotionModel? Potion { get; private set; }

    /// <summary>Custom merchant offers stay available when inventory is full, until taken or skipped.</summary>
    public bool IsOptionalMerchantChoice { get; internal set; }

    public override bool CanTake => !IsOptionalMerchantChoice || Player.PotionSlots.Contains(null);

    public PotionReward(Player player) : base(player)
    {
    }

    public PotionReward(PotionModel potion, Player player) : base(player)
    {
        ArgumentNullException.ThrowIfNull(potion);
        potion.AssertMutable();
        Potion = potion;
    }

    public override void Populate(IRunState runState)
        => Populate(
            runState,
            Player.PlayerRng.ForCurrentScopeOrSemanticKey(
                PlayerRngType.Rewards,
                $"{Player.CurrentSemanticLocationKey}/reward/slot=potion"));

    internal void Populate(IRunState runState, Rng rng)
    {
        if (Potion is not null)
        {
            return;
        }

        Potion = PotionFactory.CreateRandomOutOfCombat(Player, rng);
    }

    protected override async Task OnTake()
    {
        if (Potion is not null)
        {
            await PotionCmd.TryToProcure(Potion, Player);
        }
    }
}
