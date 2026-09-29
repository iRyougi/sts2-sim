using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public readonly record struct GeneratedCardSpec(
    int EnergyCost,
    int StarCost,
    CardType Type,
    CardRarity Rarity,
    TargetType TargetType,
    bool IsColorless,
    bool IsXEnergyCost,
    bool IsXStarCost,
    IReadOnlyCollection<CardKeyword> Keywords,
    decimal Damage,
    int HitCount,
    decimal Block,
    int Draw,
    int GainEnergy,
    int GainStars,
    decimal Weak,
    decimal Vulnerable,
    decimal Strength,
    decimal Vigor,
    decimal Forge,
    decimal UpgradeDamage,
    decimal UpgradeBlock,
    int UpgradeDraw,
    int UpgradeStars,
    decimal UpgradeVigor,
    decimal UpgradeForge,
    bool ReduceCost,
    CardKeyword? AddKeyword,
    CardKeyword? RemoveKeyword,
    int UpgradeGainEnergy = 0,
    decimal UpgradeWeak = 0m,
    decimal UpgradeVulnerable = 0m,
    decimal UpgradeStrength = 0m,
    CardChoiceBaseValues? ChoiceBaseValues = null,
    CardChoiceBaseValues ChoiceUpgradeValues = default);

/// <summary>
/// Shared execution template for Plan 06b cards composed from damage, block, draw, resources,
/// standard powers, generated powers, and Forge. 偏离 #91：依赖选择、牌史、自动回手、Replay、
/// 动态公式或专属 hook 的复杂效果目前只保留可组合的核心数值与状态载荷。
/// </summary>
public abstract class GeneratedCardModel : CardModel, ICardChoiceValueProvider, ICardChoiceBaseValueProvider,
    ICardDamageVariableProvider
{
    protected abstract GeneratedCardSpec Spec { get; }

    // Only these source cards declare DamageVar. CalculatedDamageVar cards override this
    // method with their live Calculate(null) value; Spec.Damage alone is not a DynamicVar.
    public virtual bool TryGetThrashDamageVariable(out decimal amount)
    {
        if (this is not (AstralPulse or BeatIntoShape or CelestialMight or Comet or
            CrushUnder or Devastate or DramaticEntrance or DyingStar or Exterminate or
            Fisticuffs or FlashOfSteel or GammaBlast or GuidingStar or HeavenlyDrill or
            Knockdown or MeteorShower or Omnislice or SevenStars or SolarStrike or
            Squash or Stardust or StrikeSilent or TagTeam or UltimateStrike or Volley or
            WroughtInWar))
        {
            amount = 0m;
            return false;
        }

        amount = Spec.Damage + (IsUpgraded ? Spec.UpgradeDamage : 0m);
        return true;
    }

    // Spec declarations are only for literal values that do not change during combat (apart
    // from upgrades). Dynamic cards override this getter and read their current source fields.
    // Execution effects may use aliases/calculated variables and cannot establish literal keys.
    // Null falls back only for types in the frozen audited snapshot; unknown types must declare.
    public virtual CardChoiceBaseValues? CardChoiceBaseValues =>
        Spec.ChoiceBaseValues?.WithUpgrade(Spec.ChoiceUpgradeValues, CurrentUpgradeLevel);

    public override bool GainsBlock => Spec.Block > 0m || Spec.UpgradeBlock > 0m;

    public double CardChoiceValue =>
        (double)(Spec.Damage + (IsUpgraded ? Spec.UpgradeDamage : 0m)) +
        (double)(Spec.Block + (IsUpgraded ? Spec.UpgradeBlock : 0m)) * 0.8d +
        (Spec.Draw + (IsUpgraded ? Spec.UpgradeDraw : 0)) * 3d +
        (Type == CardType.Power ? 8d : 0d);

    public override CardType Type => Spec.Type;

    public override CardRarity Rarity => Spec.Rarity;

    public override TargetType TargetType => Spec.TargetType;

    public override bool IsColorless => Spec.IsColorless;

    protected override int CanonicalEnergyCost => Spec.EnergyCost;

    protected override int CanonicalStarCost => Spec.IsXStarCost ? 0 : Spec.StarCost == 0 ? -1 : Spec.StarCost;

    protected override bool IsXEnergyCost => Spec.IsXEnergyCost;

    protected override bool IsXStarCost => Spec.IsXStarCost;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => Spec.Keywords;

    protected override void OnUpgrade()
    {
        if (Spec.ReduceCost)
        {
            ReduceEnergyCost(1);
        }
        if (Spec.AddKeyword is { } addKeyword)
        {
            AddKeyword(addKeyword);
        }
        if (Spec.RemoveKeyword is { } removeKeyword)
        {
            RemoveKeyword(removeKeyword);
        }
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal damage = Spec.Damage + (IsUpgraded ? Spec.UpgradeDamage : 0m);
        int hitCount = Spec.HitCount;
        if (Spec.IsXEnergyCost)
        {
            hitCount = Sts2Sim.Core.Hooks.Hook.ModifyXValue(
                CombatState!,
                this,
                cardPlay.Resources.EnergyXValue);
        }
        else if (Spec.IsXStarCost)
        {
            hitCount = Sts2Sim.Core.Hooks.Hook.ModifyXValue(
                CombatState!,
                this,
                cardPlay.Resources.StarXValue);
        }

        if (damage > 0m && hitCount > 0)
        {
            var attack = DamageCmd.Attack(damage).FromCard(this, cardPlay).WithHitCount(hitCount);
            switch (TargetType)
            {
                case TargetType.AllEnemies:
                    await attack.TargetingAllOpponents(CombatState!).Execute();
                    break;
                case TargetType.RandomEnemy:
                    await attack.TargetingRandomOpponents(CombatState!).Execute();
                    break;
                default:
                    ArgumentNullException.ThrowIfNull(cardPlay.Target);
                    await attack.Targeting(cardPlay.Target).Execute();
                    break;
            }
        }

        decimal block = Spec.Block + (IsUpgraded ? Spec.UpgradeBlock : 0m);
        if (block > 0m)
        {
            IEnumerable<Creature> recipients = TargetType switch
            {
                TargetType.AllAllies => CombatState!.Allies,
                TargetType.AnyAlly => new[] { cardPlay.Target ?? Owner.Creature },
                _ => new[] { Owner.Creature },
            };
            foreach (Creature recipient in recipients)
            {
                await CreatureCmd.GainBlock(CombatState!, recipient, block, ValueProp.Move, this, cardPlay);
            }
        }

        int draw = Spec.Draw + (IsUpgraded ? Spec.UpgradeDraw : 0);
        if (draw > 0)
        {
            await CardPileCmd.Draw(CombatState!, draw, Owner, fromHandDraw: false);
        }

        Owner.PlayerCombatState!.GainEnergy(
            Spec.GainEnergy + (IsUpgraded ? Spec.UpgradeGainEnergy : 0));
        await PlayerCmd.GainStars(Spec.GainStars + (IsUpgraded ? Spec.UpgradeStars : 0), Owner);

        foreach (Creature target in DebuffTargets(cardPlay))
        {
            decimal weak = Spec.Weak + (IsUpgraded ? Spec.UpgradeWeak : 0m);
            if (weak > 0m)
            {
                await PowerCmd.Apply<WeakPower>(CombatState!, target, weak, Owner.Creature, this);
            }
            decimal vulnerable = Spec.Vulnerable + (IsUpgraded ? Spec.UpgradeVulnerable : 0m);
            if (vulnerable > 0m)
            {
                await PowerCmd.Apply<VulnerablePower>(CombatState!, target, vulnerable, Owner.Creature, this);
            }
        }

        decimal strength = Spec.Strength + (IsUpgraded ? Spec.UpgradeStrength : 0m);
        if (strength > 0m)
        {
            Creature recipient = cardPlay.Target ?? Owner.Creature;
            await PowerCmd.Apply<StrengthPower>(CombatState!, recipient, strength, Owner.Creature, this);
        }

        if (GeneratedPowerEffects.TryGet(GetType(), out GeneratedPowerEffects.Effect? powerEffect))
        {
            Creature recipient = cardPlay.Target ?? Owner.Creature;
            decimal amount = powerEffect.Amount + (IsUpgraded ? powerEffect.UpgradeAmount : 0m);
            await PowerCmd.Apply(CombatState!, powerEffect.PowerType, recipient, amount, Owner.Creature, this);
        }

        decimal vigor = Spec.Vigor + (IsUpgraded ? Spec.UpgradeVigor : 0m);
        if (vigor > 0m)
        {
            await PowerCmd.Apply<VigorPower>(CombatState!, Owner.Creature, vigor, Owner.Creature, this);
        }
        decimal forge = Spec.Forge + (IsUpgraded ? Spec.UpgradeForge : 0m);
        if (forge > 0m)
        {
            await ForgeCmd.Forge(forge, Owner, this);
        }
    }

    private IEnumerable<Creature> DebuffTargets(CardPlay cardPlay)
    {
        return TargetType == TargetType.AllEnemies
            ? CombatState!.Enemies
            : cardPlay.Target is null ? Array.Empty<Creature>() : new[] { cardPlay.Target };
    }
}
