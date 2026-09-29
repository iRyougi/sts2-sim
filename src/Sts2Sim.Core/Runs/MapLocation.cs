using Sts2Sim.Core.Map;

namespace Sts2Sim.Core.Runs;

/// <summary>
/// Act index plus map coordinate. Multiplayer packet serialization is intentionally not ported.
/// </summary>
public struct MapLocation : IEquatable<MapLocation>
{
    public int actIndex;
    public MapCoord? coord;

    public MapLocation(MapCoord? coord, int actIndex)
    {
        this.coord = coord;
        this.actIndex = actIndex;
    }

    public bool Equals(MapLocation other) => actIndex == other.actIndex && coord.Equals(other.coord);

    public override bool Equals(object? obj) => obj is MapLocation other && Equals(other);

    public override int GetHashCode() => (actIndex, coord?.col, coord?.row).GetHashCode();

    public override string ToString() => $"act {actIndex} coord ({(coord.HasValue ? $"{coord.Value.col}, {coord.Value.row}" : "null")})";
}
