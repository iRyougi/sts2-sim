using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Entities.Rngs;

namespace Sts2Sim.Core.Rewards;

/// <summary>A reward that lets the player choose one card option or skip.</summary>
public sealed class CardReward : Reward
{
    private const int OptionCount = 3;

    private readonly object _resolutionLock = new();
    private readonly CardRarityOddsType _oddsType;
    private readonly IReadOnlyList<CardModel>? _explicitOptions;
    private readonly CardCreationOptions? _creationOptions;
    private readonly CardCreationOptions? _rerollOptions;
    private readonly int _optionCount = OptionCount;
    private bool _isPopulated;
    private bool _hasBeenRerolled;
    private Task? _resolutionTask;
    private object? _resolutionIdentity;
    private IReadOnlyList<CardRewardAlternative>? _alternatives;
    private RelicModel[] _alternativeRelics = [];

    public IReadOnlyList<CardModel> Options { get; private set; } = Array.Empty<CardModel>();
    public CardModel? SelectedOption { get; private set; }
    public bool CanReroll { get; private set; }

    public IReadOnlyList<CardRewardAlternative> Alternatives
    {
        get
        {
            if (IsResolved || !_isPopulated) return Array.Empty<CardRewardAlternative>();
            if (_alternatives is null || !_alternativeRelics.SequenceEqual(Player.Relics))
            {
                var alternatives = new List<CardRewardAlternative>();
                Hooks.Hook.ModifyCardRewardAlternatives(Player.RunState, Player, this, alternatives);
                _alternatives = alternatives.AsReadOnly();
                _alternativeRelics = Player.Relics.ToArray();
            }
            return _alternatives;
        }
    }


    public CardReward(Player player, CardRarityOddsType oddsType) : base(player)
    {
        _oddsType = oddsType;
    }

    public CardReward(Player player, CardCreationOptions creationOptions, int optionCount = OptionCount) : base(player)
    {
        ArgumentNullException.ThrowIfNull(creationOptions);
        ArgumentOutOfRangeException.ThrowIfNegative(optionCount);
        _creationOptions = creationOptions.WithFlags(CardCreationFlags.IsCardReward);
        _optionCount = optionCount;
    }

    public CardReward(Player player, IReadOnlyList<CardModel> options,
        CardCreationOptions? rerollOptions = null) : base(player)
    {
        ArgumentNullException.ThrowIfNull(options);
        CardModel[] copy = options.ToArray();
        if (copy.Any(option => option is null))
        {
            throw new ArgumentException("Card reward options cannot contain null.", nameof(options));
        }

        _oddsType = CardRarityOddsType.None;
        _explicitOptions = Array.AsReadOnly(copy);
        _optionCount = copy.Length;
        _rerollOptions = rerollOptions?.WithFlags(CardCreationFlags.IsCardReward);
    }

    private CardReward(Player player) : base(player)
    {
        _oddsType = CardRarityOddsType.None;
        IsResolved = true;
        _resolutionTask = Task.CompletedTask;
    }

    // CardRarityOddsType.None cannot be populated, so the absent custom card slot is resolved explicitly.
    internal static CardReward CreateResolvedEmpty(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return new CardReward(player);
    }

    public override void Populate(IRunState runState)
    {
        if ((_explicitOptions is not null || _creationOptions is not null) && _isPopulated)
        {
            return;
        }

        if (_hasBeenRerolled && _explicitOptions is not null)
        {
            Options = CardFactory.CreateForReward(Player, _optionCount,
                _rerollOptions ?? throw new InvalidOperationException(
                    "Explicit card reward has no reroll options."));
        }
        else if (_creationOptions is not null)
        {
            Options = CardFactory.CreateForReward(Player, _optionCount, _creationOptions);
        }
        else
        {
            var creationOptions = new CardCreationOptions([Player.Character.CardPool],
                _explicitOptions is null ? CardCreationSource.Encounter : CardCreationSource.Other, _oddsType)
                .WithFlags(CardCreationFlags.IsCardReward);
            if (Player.PlayerRng.UsesSemanticKeys)
            {
                creationOptions.WithRngOverride(
                    Player.PlayerRng.ForCurrentScopeOrSemanticKey(
                        PlayerRngType.Rewards,
                        $"{Player.CurrentSemanticLocationKey}/reward/slot=card/source={creationOptions.Source}"));
            }
            if (_explicitOptions is null)
            {
                creationOptions.WithFlags(CardCreationFlags.IsFromCombat);
                IReadOnlyList<CardModel> cards = Player.PlayerRng.UsesSemanticKeys
                    ? CardFactory.CreateForReward(Player, OptionCount, creationOptions)
                    : CardFactory.CreateForReward(Player, OptionCount, _oddsType);
                Options = CardFactory.ModifyRewardOptions(runState, Player, cards, creationOptions);
            }
            else Options = CardFactory.ModifyRewardOptions(runState, Player, _explicitOptions, creationOptions);
        }

        _isPopulated = true;
        CanReroll = !_hasBeenRerolled &&
            (_explicitOptions is null || _rerollOptions is not null) &&
            Hooks.Hook.CanRerollCardReward(runState, Player, this);
    }

    public Task SelectOption(CardModel chosen) => Resolve(chosen, () =>
    {
        if (!Options.Any(option => ReferenceEquals(option, chosen)))
            throw new InvalidOperationException("Chosen card is not one of the offered options.");
    }, async () =>
    {
        var copy = (CardModel)chosen.MutableClone();
        copy.AssignOwner(Player);
        await Commands.CardPileCmd.AddToDeck(copy);
        SelectedOption = chosen;
    });

    /// <summary>Abandons the reward permanently; this is not the game's reopenable UI cancel.</summary>
    public Task Skip() => Resolve(null, () => { }, () => Task.CompletedTask);

    public Task SelectAlternative(CardRewardAlternative alternative)
    {
        ArgumentNullException.ThrowIfNull(alternative);
        if (!alternative.CompletesReward)
        {
            if (IsResolved || !Alternatives.Any(offered => ReferenceEquals(offered, alternative)) ||
                !alternative.IsAvailable)
                throw new InvalidOperationException("Alternative is not available on this card reward.");
            return alternative.Select();
        }
        return Resolve(alternative, () =>
        {
            if (!Alternatives.Any(offered => ReferenceEquals(offered, alternative)) || !alternative.IsAvailable)
                throw new InvalidOperationException("Alternative is not available on this card reward.");
        }, alternative.Select);
    }

    public Task Reroll()
    {
        lock (_resolutionLock)
        {
            if (IsResolved || _resolutionTask is not null || !CanReroll)
                throw new InvalidOperationException("This card reward cannot be rerolled.");
            CanReroll = false;
            _hasBeenRerolled = true;
        }
        _isPopulated = false;
        _alternatives = null;
        Populate(Player.RunState);
        return Task.CompletedTask;
    }
    private Task Resolve(object? identity, Action validate, Func<Task> execute)
    {
        TaskCompletionSource completion;
        lock (_resolutionLock)
        {
            if (_resolutionTask is not null)
                return ReferenceEquals(_resolutionIdentity, identity) ? _resolutionTask :
                    throw new InvalidOperationException("This reward was already resolved by a different operation.");
            if (IsResolved)
                throw new InvalidOperationException("This reward is already resolved.");
            validate();
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _resolutionIdentity = identity;
            // Publish before any synchronous hook can reenter and replay the side effects.
            _resolutionTask = completion.Task;
        }
        _ = ExecuteOnce(execute, completion);
        return completion.Task;
    }

    private async Task ExecuteOnce(Func<Task> execute, TaskCompletionSource completion)
    {
        try
        {
            await execute();
            IsResolved = true;
            completion.SetResult();
        }
        catch (Exception exception)
        {
            // Reward side effects are not transactional. Retain the failed task, never retry them.
            IsResolved = true;
            completion.SetException(exception);
        }
    }
}
