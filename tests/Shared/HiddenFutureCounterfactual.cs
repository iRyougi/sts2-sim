using System.Reflection;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.TestSupport;

internal static class HiddenFutureCounterfactual
{
    internal static void Change(RunState run)
    {
        foreach (PropertyInfo property in run.Rng.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            if (property.PropertyType == typeof(Rng)) ((Rng)property.GetValue(run.Rng)!).NextInt(97);
        foreach (PropertyInfo property in run.Players[0].PlayerRng.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            if (property.PropertyType == typeof(Rng)) ((Rng)property.GetValue(run.Players[0].PlayerRng)!).NextInt(97);
        ReverseUnvisited<EncounterDefinition>(run, "_normalEncounterSequence", "_normalEncountersVisited");
        ReverseUnvisited<EncounterDefinition>(run, "_eliteEncounterSequence", "_eliteEncountersVisited");
        ReverseUnvisited<Type>(run, "_eventSequence", "_eventsVisited");
    }

    private static void ReverseUnvisited<T>(RunState run, string sequenceField, string cursorField)
    {
        List<T> sequence = PrivateList<T>(run, sequenceField);
        int cursor = (int)typeof(RunState).GetField(cursorField, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(run)!;
        sequence.Reverse(cursor, sequence.Count - cursor);
    }

    internal static List<T> PrivateList<T>(RunState run, string field) =>
        (List<T>)typeof(RunState).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(run)!;
}
