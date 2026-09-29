namespace Sts2Sim.Core.Commands;

/// <summary>Awaitable gameplay timing in the headless host, which has no Godot timescale.</summary>
public static class Cmd
{
    public static Task Wait(float seconds) =>
        seconds <= 0f ? Task.CompletedTask : Task.Delay(TimeSpan.FromSeconds(seconds));
}
