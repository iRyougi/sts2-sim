using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Afflictions;

namespace Sts2Sim.Core.Content;

/// <summary>
/// Single source of truth for concrete game-content model types registered with <see cref="ModelDb"/>.
/// The deterministic full-name ordering makes IDs stable across all entry points.
/// </summary>
public static class ContentRegistry
{
    private static readonly Type[] ContentBaseTypes =
    {
        typeof(CardModel),
        typeof(AfflictionModel),
        typeof(CharacterModel),
        typeof(EnchantmentModel),
        typeof(EventModel),
        typeof(MonsterModel),
        typeof(OrbModel),
        typeof(PotionModel),
        typeof(PowerModel),
        typeof(RelicModel),
    };

    public static IReadOnlyList<Type> AllTypes { get; } = typeof(ContentRegistry).Assembly
        .GetTypes()
        .Where(type =>
            !type.IsAbstract &&
            ContentBaseTypes.Any(baseType => baseType.IsAssignableFrom(type)))
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .ToArray();
}
