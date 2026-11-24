using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>
/// Verbatim port of <c>MegaCrit.Sts2.Core.Models.Cards.Abundance</c>: three upgraded Power candidates
/// from the character pool (full-pool shuffle), then <c>CardSelectCmd.FromChooseACardScreen</c> without
/// <c>canSkip</c>, so exactly one candidate must be chosen. The choice goes through the combat
/// card-selection decision source (same mechanism as Toolbox).
/// </summary>
public sealed class Abundance : CardModel
{
    private List<CardModel> _generatedCandidates = new();

    public IReadOnlyList<CardModel> GeneratedCandidates => _generatedCandidates;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.Self;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Exhaust };

    protected override void OnUpgrade() => ReduceEnergyCost(1);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        _generatedCandidates = CardFactory.GetDistinctForCombat(
            Owner,
            Owner.Character.CardPool.GetUnlockedCards(Owner.UnlockState, Owner.RunState.Players.Count > 1)
                .Where(card => card.Type == CardType.Power),
            3,
            CombatState!.RunState.Rng.CombatCardGeneration).ToList();
        foreach (CardModel candidate in _generatedCandidates)
        {
            candidate.Upgrade();
        }

        // FromChooseACardScreen(canSkip: false): one of the candidates must be chosen; an empty result
        // (empty candidate list) adds nothing.
        CardModel? chosen = (await CardSelectCmd.SelectCardsAsync(
            CombatState!, Owner, _generatedCandidates, 1, 1, this)).FirstOrDefault();
        if (chosen is null) return;
        chosen.AssignOwner(Owner);
        chosen.SetToFreeThisTurn();     // 游戏 Abundance：SetToFreeThisTurn（本回合内或打出前免费；不覆盖 X 费用）
        await CardPileCmd.Generate(CombatState!, chosen, PileType.Hand);
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _generatedCandidates = new List<CardModel>();
    }

    internal override void RestoreCombatCloneReferencesFrom(
        CardModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        var sourceAbundance = (Abundance)source;
        _generatedCandidates = sourceAbundance._generatedCandidates
            .Select(candidate => cardMap.TryGetValue(candidate, out CardModel? mapped)
                ? mapped
                : candidate.CloneForCombat(candidate.HasOwner ? Owner : null))
            .ToList();
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_generatedCandidates.Count);
        foreach (CardModel candidate in _generatedCandidates)
        {
            builder.Append(candidate.Id);
            builder.Append(candidate.CurrentUpgradeLevel);
        }
    }
}
