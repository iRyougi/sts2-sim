namespace Sts2Sim.Core.Random;

internal sealed record KeyedRngDraw(
    string StreamName,
    string SemanticKey,
    int DrawOrdinal,
    string Operation,
    string Result);
