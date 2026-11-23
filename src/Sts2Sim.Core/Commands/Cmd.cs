namespace Sts2Sim.Core.Commands;

/// <summary>Headless timing commands complete immediately, matching native noninteractive mode.</summary>
public static class Cmd
{
    public static Task Wait(float seconds) => Task.CompletedTask;
}
