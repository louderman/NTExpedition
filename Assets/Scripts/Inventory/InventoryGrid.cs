using System;
using System.Collections.Generic;

public struct GridCell
{
    public int X, Y;
    public GridCell(int x, int y) { X = x; Y = y; }
}

// Pure grid logic: no scene, input or UI dependencies.
public class InventoryGrid<T> where T : class
{
    readonly T[,] cells;
    public int Width { get { return cells.GetLength(0); } }
    public int Height { get { return cells.GetLength(1); } }
    public InventoryGrid(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException("Grid dimensions must be positive");
        cells = new T[width, height];
    }
    public T Get(int x, int y) { return x >= 0 && y >= 0 && x < Width && y < Height ? cells[x,y] : null; }
    public static GridCell[] Rotate(GridCell[] shape, int rotation)
    {
        if (shape == null || shape.Length == 0) return new GridCell[0];
        int turns = ((rotation % 4) + 4) % 4;
        var result = new GridCell[shape.Length];
        int minX = int.MaxValue, minY = int.MaxValue;
        for (int i = 0; i < shape.Length; i++)
        {
            int x = shape[i].X, y = shape[i].Y;
            for (int j = 0; j < turns; j++) { int oldX = x; x = -y; y = oldX; }
            result[i] = new GridCell(x,y);
            minX = Math.Min(minX,x); minY = Math.Min(minY,y);
        }
        for (int i = 0; i < result.Length; i++) result[i] = new GridCell(result[i].X-minX,result[i].Y-minY);
        return result;
    }
    public bool CanPlace(T item, GridCell[] shape, int x, int y, int rotation)
    {
        if (item == null || shape == null || shape.Length == 0) return false;
        var offsets = Rotate(shape, rotation);
        var unique = new HashSet<long>();
        foreach (var cell in offsets)
        {
            int px = x + cell.X, py = y + cell.Y;
            if (px < 0 || py < 0 || px >= Width || py >= Height) return false;
            if (!unique.Add(((long)px << 32) | (uint)py)) return false;
            if (cells[px,py] != null && !ReferenceEquals(cells[px,py], item)) return false;
        }
        return true;
    }
    public bool TryPlace(T item, GridCell[] shape, int x, int y, int rotation)
    {
        if (!CanPlace(item, shape, x, y, rotation)) return false;
        Remove(item);
        foreach (var cell in Rotate(shape, rotation)) cells[x+cell.X,y+cell.Y] = item;
        return true;
    }
    public bool Remove(T item)
    {
        if (item == null) return false;
        bool found = false;
        for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            if (ReferenceEquals(cells[x,y], item)) { cells[x,y] = null; found = true; }
        return found;
    }
    public bool TryAutoPlace(T item, GridCell[] shape, out int resultX, out int resultY, out int resultRotation)
    {
        for (int r = 0; r < 4; r++) for (int y = 0; y < Height; y++) for (int x = 0; x < Width; x++)
            if (TryPlace(item, shape, x, y, r)) { resultX = x; resultY = y; resultRotation = r; return true; }
        resultX = resultY = resultRotation = 0; return false;
    }
}
