namespace Sts2Sim.Core.Map;

/// <summary>
/// 地图节点生成后的网格布局调整（<c>MegaCrit.Sts2.Core.Map.MapPostProcessing</c>）。逐字移植。
///
/// 偏离 #52 修正：这三步此前被判定为"纯视觉、不消耗 RNG"而故意不移植——不消耗 RNG 这一点是对的，
/// 但"纯视觉"是错的：它们会直接改写 <see cref="MapPoint.coord"/> 的列号，而这个列号之后被当作节点
/// 身份使用（Plan07 仿真-真实校验发现：不移植会导致 sts2-ai 记录的列号和真实游戏在多分支场景下
/// ——例如 <c>WingedBoots</c>"无视路线"——对不上，即便两边的图连接关系完全一致）。因为图连接关系
/// （<c>Children</c>/<c>parents</c>）本身不受这三步影响，单一可走节点的场景（绝大多数楼层）此前
/// 感知不到这个偏差。
/// </summary>
public static class MapPostProcessing
{
    /// <summary>如果最左或最右两列都是空的，把整个网格向那一侧平移，让地图视觉居中。</summary>
    public static MapPoint?[,] CenterGrid(MapPoint?[,] grid)
    {
        int columns = grid.GetLength(0);
        int rows = grid.GetLength(1);

        bool leftEmpty = IsColumnEmpty(grid, 0) && IsColumnEmpty(grid, 1);
        bool rightEmpty = IsColumnEmpty(grid, columns - 1) && IsColumnEmpty(grid, columns - 2);

        int shift = 0;
        if (leftEmpty && !rightEmpty)
        {
            shift = -1;
        }
        else if (!leftEmpty && rightEmpty)
        {
            shift = 1;
        }

        if (shift == 0)
        {
            return grid;
        }

        if (shift > 0)
        {
            for (int row = 0; row < rows; row++)
            {
                for (int col = columns - 1; col >= 0; col--)
                {
                    MapPoint? point = grid[col, row];
                    grid[col, row] = null;
                    int target = col + shift;
                    if (target < columns)
                    {
                        grid[target, row] = point;
                        if (point is not null)
                        {
                            point.coord.col = target;
                        }
                    }
                }
            }
        }
        else
        {
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    MapPoint? point = grid[col, row];
                    grid[col, row] = null;
                    int target = col + shift;
                    if (target >= 0)
                    {
                        grid[target, row] = point;
                        if (point is not null)
                        {
                            point.coord.col = target;
                        }
                    }
                }
            }
        }

        return grid;
    }

    /// <summary>把单入单出、且父子都偏向同一侧的"拐点"节点朝父子方向拉直一格。</summary>
    public static MapPoint?[,] StraightenPaths(MapPoint?[,] grid)
    {
        int columns = grid.GetLength(0);
        int rows = grid.GetLength(1);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                MapPoint? point = grid[col, row];
                if (point is null || point.parents.Count != 1 || point.Children.Count != 1)
                {
                    continue;
                }

                MapPoint parent = point.parents.First();
                MapPoint child = point.Children.First();

                bool bothToTheRight = point.coord.col < child.coord.col && point.coord.col < parent.coord.col;
                bool bothToTheLeft = point.coord.col > child.coord.col && point.coord.col > parent.coord.col;

                if (bothToTheRight && col < columns - 1)
                {
                    int target = col + 1;
                    if (grid[target, row] != null)
                    {
                        continue;
                    }

                    point.coord.col = target;
                    grid[col, row] = null;
                    grid[target, row] = point;
                }

                if (bothToTheLeft && col > 0)
                {
                    int target = col - 1;
                    if (grid[target, row] == null)
                    {
                        point.coord.col = target;
                        grid[col, row] = null;
                        grid[target, row] = point;
                    }
                }
            }
        }

        return grid;
    }

    /// <summary>同一行内的节点在不违反"和父/子最多相差一列"的约束下，尽量互相拉开间距。</summary>
    public static MapPoint?[,] SpreadAdjacentMapPoints(MapPoint?[,] grid)
    {
        int columns = grid.GetLength(0);
        int rows = grid.GetLength(1);

        for (int row = 0; row < rows; row++)
        {
            var rowPoints = new List<MapPoint>(rows);
            for (int col = 0; col < columns; col++)
            {
                if (grid[col, row] is { } point)
                {
                    rowPoints.Add(point);
                }
            }

            bool changed;
            do
            {
                changed = false;
                foreach (MapPoint point in rowPoints)
                {
                    int col = point.coord.col;
                    HashSet<int> allowed = GetAllowedPositions(point, columns);
                    int bestCol = col;
                    int bestGap = ComputeGap(col, rowPoints, point);

                    foreach (int candidate in allowed)
                    {
                        if (candidate == col || (grid[candidate, row] != null && grid[candidate, row] != point))
                        {
                            continue;
                        }

                        int gap = ComputeGap(candidate, rowPoints, point);
                        if (gap > bestGap)
                        {
                            bestCol = candidate;
                            bestGap = gap;
                        }
                    }

                    if (bestCol != col)
                    {
                        grid[col, row] = null;
                        grid[bestCol, row] = point;
                        point.coord.col = bestCol;
                        changed = true;
                    }
                }
            }
            while (changed);
        }

        return grid;
    }

    private static bool IsColumnEmpty(MapPoint?[,] grid, int col)
    {
        int rows = grid.GetLength(1);
        for (int row = 0; row < rows; row++)
        {
            if (grid[col, row] != null)
            {
                return false;
            }
        }

        return true;
    }

    private static HashSet<int> GetNeighborAllowedPositions(int column, int totalColumns)
    {
        var result = new HashSet<int>(3);
        for (int offset = -1; offset <= 1; offset++)
        {
            int candidate = column + offset;
            if (candidate >= 0 && candidate < totalColumns)
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    private static HashSet<int> GetAllowedPositions(MapPoint node, int totalColumns)
    {
        var allowed = new HashSet<int>(Enumerable.Range(0, totalColumns));
        foreach (MapPoint parent in node.parents)
        {
            allowed.IntersectWith(GetNeighborAllowedPositions(parent.coord.col, totalColumns));
        }

        foreach (MapPoint child in node.Children)
        {
            allowed.IntersectWith(GetNeighborAllowedPositions(child.coord.col, totalColumns));
        }

        return allowed;
    }

    private static int ComputeGap(int candidateCol, List<MapPoint> rowPoints, MapPoint current)
    {
        int min = int.MaxValue;
        foreach (MapPoint other in rowPoints)
        {
            if (!ReferenceEquals(other, current))
            {
                min = Math.Min(min, Math.Abs(candidateCol - other.coord.col));
            }
        }

        return min;
    }
}
