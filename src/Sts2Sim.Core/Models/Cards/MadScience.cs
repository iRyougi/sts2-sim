using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public enum MadScienceRider
{
    None,
    Sapping,
    Violence,
    Choking,
    Energized,
    Wisdom,
    Chaos,
    Expertise,
    Curious,
    Improvement,
}

/// <summary>
/// Tinker Time's configurable event card.
/// 偏离 #256：无头模拟器不移植随类型/词条变化的美术、标题和动态描述；类型、目标、数值与九种词条玩法均保留。
/// </summary>
public sealed class MadScience : CardModel, ICardDamageVariableProvider
{
    public override bool GainsBlock => Type == CardType.Skill;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        // Native DamageVar exists for all configured card types.
        amount = 12m;
        return true;
    }

    private CardType _type = CardType.Attack;

    public MadScienceRider Rider { get; private set; }
    public override CardType Type => _type;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => Type == CardType.Attack ? TargetType.AnyEnemy : TargetType.Self;
    public override bool CanBeGeneratedByModifiers => false;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => 1;

    public void Configure(CardType type, MadScienceRider rider)
    {
        AssertMutable();
        if (type is not (CardType.Attack or CardType.Skill or CardType.Power))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }
        _type = type;
        Rider = rider;
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Innate);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        switch (Type)
        {
            case CardType.Attack:
                await PlayAttackAsync(cardPlay);
                break;
            case CardType.Skill:
                await PlaySkillAsync(cardPlay);
                break;
            case CardType.Power:
                await PlayPowerAsync();
                break;
        }
    }

    private async Task PlayAttackAsync(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = Rider == MadScienceRider.Violence ? 3 : 1;
        for (int i = 0; i < hits; i++)
        {
            await DamageCmd.Attack(12m).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        }
        if (Rider == MadScienceRider.Sapping)
        {
            await PowerCmd.Apply<WeakPower>(CombatState!, cardPlay.Target, 2m, Owner.Creature, this);
            await PowerCmd.Apply<VulnerablePower>(CombatState!, cardPlay.Target, 2m, Owner.Creature, this);
        }
        else if (Rider == MadScienceRider.Choking)
        {
            await PowerCmd.Apply<StranglePower>(CombatState!, cardPlay.Target, 6m, Owner.Creature, this);
        }
    }

    private async Task PlaySkillAsync(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, 8m, ValueProp.Move, this, cardPlay);
        switch (Rider)
        {
            case MadScienceRider.Energized:
                await PlayerCmd.GainEnergy(2m, Owner);
                break;
            case MadScienceRider.Wisdom:
                await CardPileCmd.Draw(CombatState!, 3, Owner, fromHandDraw: false);
                break;
            case MadScienceRider.Chaos:
                await GenerateChaosCardAsync();
                break;
        }
    }

    private async Task GenerateChaosCardAsync()
    {
        CardModel? generated = CardFactory.GetDistinctForCombat(
            Owner,
            Owner.Character.CardPool.GetUnlockedCards(
                Owner.UnlockState,
                Owner.RunState.Players.Count > 1),
            1,
            CombatState!.RunState.Rng.CombatCardGeneration).FirstOrDefault();
        if (generated is null)
        {
            return;
        }
        generated.MakeTemporaryFreeThisTurn();
        await CardPileCmd.Generate(CombatState, generated, PileType.Hand);
    }

    private async Task PlayPowerAsync()
    {
        switch (Rider)
        {
            case MadScienceRider.Expertise:
                await PowerCmd.Apply<StrengthPower>(CombatState!, Owner.Creature, 2m, Owner.Creature, this);
                await PowerCmd.Apply<DexterityPower>(CombatState!, Owner.Creature, 2m, Owner.Creature, this);
                break;
            case MadScienceRider.Curious:
                await PowerCmd.Apply<CuriousPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
                break;
            case MadScienceRider.Improvement:
                await PowerCmd.Apply<ImprovementPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
                break;
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append((int)Rider);
}
