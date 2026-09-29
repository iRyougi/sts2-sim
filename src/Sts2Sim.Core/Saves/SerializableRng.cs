namespace Sts2Sim.Core.Saves;

public record SerializableRng
{
    public int counter;
    public ulong state0;
    public ulong state1;
    public ulong state2;
    public ulong state3;
}
