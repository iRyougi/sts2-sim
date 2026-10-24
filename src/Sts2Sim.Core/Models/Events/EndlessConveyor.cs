using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.PotionPools;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class EndlessConveyor : EventModel
{
    private const int GrabCost = 40;
    private const int GoldenFyshGold = 75;
    private const int ClamRollHeal = 10;
    private const int CaviarMaxHp = 4;
    private string _lastDishId = string.Empty;
    private string _currentDishId = string.Empty;
    private int _numOfGrabs;
    private string _visibleLastDishId = string.Empty;

    public override bool IsAllowed(IRunState runState) =>
        runState.Players.All(player => player.Gold >= 120);

    protected override VisibleDecisionMetadata BuildVisibleState() => new(
        string.IsNullOrEmpty(_visibleLastDishId)
            ? [VisibleValue.Int("Gold", 40)]
            : [VisibleValue.Key("LastDishTitle", $"ENDLESS_CONVEYOR.DISHES.{_visibleLastDishId}.title")],
        [
            new("CAVIAR", [VisibleValue.Int("Gold", 40), VisibleValue.Int("CaviarMaxHp", 4)]),
            new("CLAM_ROLL", [VisibleValue.Int("Gold", 40), VisibleValue.Int("ClamRollHeal", 10)]),
            new("GOLDEN_FYSH", [VisibleValue.Int("GoldenFyshGold", 75)]),
            new("SPICY_SNAPPY", [VisibleValue.Int("Gold", 40), VisibleValue.Int("RandomUpgradeCount", 1)]),
            new("JELLY_LIVER", [VisibleValue.Int("Gold", 40), VisibleValue.Int("TransformCount", 1)]),
            new("FRIED_EEL", [VisibleValue.Int("Gold", 40), VisibleValue.Int("ObtainCardCount", 1), VisibleValue.Scope("CardPool", "Colorless")]),
            new("SEAPUNK_SALAD", [VisibleValue.Int("Gold", 40), VisibleValue.Int("ObtainCount", 1), VisibleValue.Id("Card", "FEEDING_FRENZY")]) { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<FeedingFrenzy>())] },
            new("OBSERVE_CHEF", [VisibleValue.Int("RandomUpgradeCount", 1)]),
            new("SUSPICIOUS_CONDIMENT", [VisibleValue.Int("Gold", 40), VisibleValue.Int("ObtainPotionCount", 1)]),
        ]);

    protected override void CalculateVars()
    {
        _visibleLastDishId = string.Empty;
        RollDish();
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
    [
        GrabOption(),
        new("OBSERVE_CHEF", ObserveChef),
    ];

    private async Task GrabSomethingOffTheBelt()
    {
        string displayedDishId = _currentDishId;
        if (_currentDishId != "GOLDEN_FYSH")
            await PlayerCmd.LoseGold(GrabCost, Owner, GoldLossType.Spent);

        await ResolveDish();
        RollDish();
        SetOptions(
        [
            GrabOption(),
            new("LEAVE", Leave),
        ]);
        _visibleLastDishId = displayedDishId;
    }

    private EventOption GrabOption() =>
        Owner.Gold >= GrabCost
            ? new(_currentDishId, GrabSomethingOffTheBelt)
            : new("LOCKED", null);

    private async Task ResolveDish()
    {
        switch (_currentDishId)
        {
            case "CAVIAR":
                await CreatureCmd.GainMaxHp(Owner.Creature, CaviarMaxHp);
                break;
            case "SPICY_SNAPPY":
                CardModel[] upgradable = Owner.Deck.Cards.Where(card => card.IsUpgradable).ToArray();
                if (upgradable.Length > 0) CardCmd.Upgrade(Rng.NextItem(upgradable)!);
                break;
            case "JELLY_LIVER":
                CardModel? transform = (await CardSelectCmd.SelectCardsAsync(
                    RunState,
                    Owner,
                    Owner.Deck.Cards.Where(card => card.IsTransformable),
                    1,
                    1,
                    this)).FirstOrDefault();
                if (transform is not null) await CardCmd.TransformToRandom(transform, Rng, RunState);
                break;
            case "FRIED_EEL":
                var options = CardFactory.CreateForReward(
                    Owner,
                    1,
                    CardCreationOptions.ForNonCombatWithDefaultOdds([ColorlessCardPool.Instance]));
                if (options.Count > 0) await CardPileCmd.AddToDeck(options[0]);
                break;
            case "SEAPUNK_SALAD":
                CardModel card = (CardModel)ModelDb.Card<FeedingFrenzy>().MutableClone();
                card.AssignOwner(Owner);
                await CardPileCmd.AddToDeck(card);
                break;
            case "SUSPICIOUS_CONDIMENT":
                IEnumerable<PotionModel> potions = Owner.Character.PotionPool.GetUnlockedPotions(Owner.UnlockState)
                    .Concat(SharedPotionPool.Instance.GetUnlockedPotions(Owner.UnlockState));
                PotionModel? potion = Owner.PlayerRng.Rewards.NextItem(potions);
                if (potion is not null)
                    OfferRewards(RewardsSet.CreateCustom(Owner,
                        potion: new PotionReward((PotionModel)potion.MutableClone(), Owner)));
                break;
            case "CLAM_ROLL":
                await CreatureCmd.Heal(Owner.Creature, ClamRollHeal);
                break;
            case "GOLDEN_FYSH":
                await PlayerCmd.GainGold(GoldenFyshGold, Owner);
                break;
            default:
                throw new InvalidOperationException($"Unknown Endless Conveyor dish '{_currentDishId}'.");
        }
    }

    private void RollDish()
    {
        _numOfGrabs++;
        if (_numOfGrabs % 5 == 0)
        {
            _lastDishId = "SEAPUNK_SALAD";
            _currentDishId = _lastDishId;
            return;
        }

        var dishes = new List<(string Id, float Weight)>
        {
            ("CAVIAR", 6f),
            ("SPICY_SNAPPY", 3f),
            ("JELLY_LIVER", 3f),
            ("FRIED_EEL", 3f),
        };
        if (Owner.PotionSlots.Any(slot => slot is null)) dishes.Add(("SUSPICIOUS_CONDIMENT", 3f));
        if (Owner.Creature.CurrentHp != Owner.Creature.MaxHp) dishes.Add(("CLAM_ROLL", 6f));
        if (_numOfGrabs > 1) dishes.Add(("GOLDEN_FYSH", 1f));
        dishes.RemoveAll(dish => dish.Id == _lastDishId);

        float total = dishes.Sum(dish => dish.Weight);
        float roll = Rng.NextFloat() * total;
        foreach ((string id, float weight) in dishes)
        {
            roll -= weight;
            if (roll < 0f)
            {
                _lastDishId = id;
                _currentDishId = id;
                return;
            }
        }

        _currentDishId = _lastDishId = dishes[^1].Id;
    }

    private Task ObserveChef()
    {
        CardModel[] upgradable = Owner.Deck.Cards.Where(card => card.IsUpgradable).ToArray();
        if (upgradable.Length > 0) CardCmd.Upgrade(Rng.NextItem(upgradable)!);
        Finish();
        return Task.CompletedTask;
    }

    private Task Leave()
    {
        Finish();
        return Task.CompletedTask;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _lastDishId = string.Empty;
        _currentDishId = string.Empty;
        _numOfGrabs = 0;
        _visibleLastDishId = string.Empty;
    }
}
