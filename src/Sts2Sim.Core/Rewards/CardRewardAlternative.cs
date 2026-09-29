namespace Sts2Sim.Core.Rewards;

/// <summary>An offered action that completes its card reward without taking a card.</summary>
public sealed class CardRewardAlternative
{
    public string OptionId { get; }
    private readonly Func<Task> _onSelect;
    private readonly Func<bool> _isAvailable;

    public CardRewardAlternative(string optionId, Func<Task> onSelect, Func<bool> isAvailable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionId);
        OptionId = optionId;
        _onSelect = onSelect ?? throw new ArgumentNullException(nameof(onSelect));
        _isAvailable = isAvailable ?? throw new ArgumentNullException(nameof(isAvailable));
    }

    internal bool IsAvailable => _isAvailable();
    internal Task Select() => _onSelect();
}
