namespace Sts2Sim.Core.Runs;

[Flags]
public enum CardCreationFlags
{
    NoRarityModification = 1,
    NoUpgradeRoll = 2,
    NoHookUpgrades = 4,
    NoModifyHooks = 8,
    NoCardPoolModifications = 16,
    NoCardModelModifications = 32,
    ForceRarityOddsChange = 64,
    IsCardReward = 128,
    IsFromCombat = 256,
    NoUpgrades = NoUpgradeRoll | NoHookUpgrades,
    NoModifications = -1,
}
