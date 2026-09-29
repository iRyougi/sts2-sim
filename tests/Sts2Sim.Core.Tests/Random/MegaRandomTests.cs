namespace Sts2Sim.Core.Tests.Random;

using Sts2Sim.Core.Random;
using Sts2Sim.Core.Saves;

public class MegaRandomTests
{
    [Theory]
    [InlineData(0uL, new ulong[] { 11091344671253066420uL, 13793997310169335082uL, 1900383378846508768uL, 7684712102626143532uL, 13521403990117723737uL })]
    [InlineData(42uL, new ulong[] { 1546998764402558742uL, 6990951692964543102uL, 12544586762248559009uL, 17057574109182124193uL, 18295552978065317476uL })]
    [InlineData(123456789uL, new ulong[] { 15127205273500847298uL, 16265768176396019016uL, 1514321867679316104uL, 9853693475100939714uL, 16001046604883718113uL })]
    [InlineData(ulong.MaxValue, new ulong[] { 10328197420357168392uL, 14156678507024973869uL, 9357971779955476126uL, 13791585006304312367uL, 10463432026814718762uL })]
    public void NextULong_MatchesPythonReference(ulong seed, ulong[] expected)
    {
        var r = new MegaRandom(seed);
        foreach (ulong e in expected) Assert.Equal(e, r.NextULong());
    }

    [Fact]
    public void NextDouble_MatchesPythonReference()
    {
        var r = new MegaRandom(42uL);
        Assert.Equal(0.08386297105988216, r.NextDouble(), 15);
        Assert.Equal(0.3789802506626686, r.NextDouble(), 15);
        Assert.Equal(0.6800434110281394, r.NextDouble(), 15);
    }

    [Fact]
    public void NextBounded_MatchesPythonReference()
    {
        var r = new MegaRandom(42uL);
        int[] expected = { 8, 37, 68, 92, 99, 76, 71, 85 };
        foreach (int e in expected) Assert.Equal(e, r.Next(100));
    }

    [Fact]
    public void SerializableState_RoundTrips_AndMatchesPythonState()
    {
        var r = new MegaRandom(7uL);
        for (int i = 0; i < 10; i++) r.NextULong();
        var s = new SerializableRng();
        r.FillSerializableState(s);
        Assert.Equal(10678822731750918588uL, s.state0);
        Assert.Equal(11966496932103979365uL, s.state1);
        Assert.Equal(10499519831635765807uL, s.state2);
        Assert.Equal(6295520318942947715uL, s.state3);

        var restored = new MegaRandom(s);
        for (int i = 0; i < 50; i++) Assert.Equal(r.NextULong(), restored.NextULong());
    }

    [Fact]
    public void Next_InvalidArgs_Throw()
    {
        var r = new MegaRandom(1uL);
        Assert.Throws<ArgumentOutOfRangeException>(() => r.Next(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => r.Next(5, 5));
    }

    [Fact]
    public void Clone_ContinuesIdenticalIndependentSequence()
    {
        var a = new MegaRandom(99uL);
        for (int i = 0; i < 13; i++) a.NextULong();
        var b = a.Clone();
        for (int i = 0; i < 50; i++) Assert.Equal(a.NextULong(), b.NextULong());
    }
}
