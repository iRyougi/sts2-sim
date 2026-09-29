namespace Sts2Sim.Core.Map;

/// <summary>地图上某点的行列坐标。逐字移植（<c>MegaCrit.Sts2.Core.Map.MapCoord</c>）。
/// 偏离 #42：不含 <c>IPacketSerializable</c>（多人序列化，未移植）。</summary>
public struct MapCoord : IEquatable<MapCoord>, IComparable<MapCoord>
{
    public int col;
    public int row;

    public MapCoord(int col, int row)
    {
        this.col = col;
        this.row = row;
    }

    public static bool operator ==(MapCoord a, MapCoord b) => a.Equals(b);
    public static bool operator !=(MapCoord a, MapCoord b) => !(a == b);

    public bool Equals(MapCoord other) => col == other.col && row == other.row;

    public override bool Equals(object? obj) => obj is MapCoord other && Equals(other);

    public override int GetHashCode() => (col, row).GetHashCode();

    public int CompareTo(MapCoord other) => (col, row).CompareTo((other.col, other.row));

    public override string ToString() => $"MapCoord ({col}, {row})";
}
