namespace Sts2Sim.Core.Rewards;

/// <summary>An offered action that completes its card reward without taking a card.</summary>
public sealed class CardRewardAlternative
{
    public string OptionId { get; }
    private readonly Func<Task> _onSelect;
    private readonly Func<bool> _isAvailable;
    internal bool CompletesReward { get; }

    public CardRewardAlternative(string optionId, Func<Task> onSelect, Func<bool> isAvailable,
        bool completesReward = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionId);
        OptionId = optionId;
        _onSelect = onSelect ?? throw new ArgumentNullException(nameof(onSelect));
        _isAvailable = isAvailable ?? throw new ArgumentNullException(nameof(isAvailable));
        CompletesReward = completesReward;
    }

    internal bool IsAvailable => _isAvailable();
    internal Task Select() => _onSelect();
}
