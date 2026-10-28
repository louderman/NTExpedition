using System;
using UnityEngine;

public class InventoryContainer : MonoBehaviour
{
    [SerializeField] Vector2Int containerDimensions = new Vector2Int(6, 4);
    public Vector2Int ContainerDimensions => containerDimensions;
    InventoryGrid<ItemComponent> grid;
    public event Action Changed;
    private void Awake() { if (containerDimensions.x > 0 && containerDimensions.y > 0) grid = new InventoryGrid<ItemComponent>(containerDimensions.x, containerDimensions.y); }
    public ItemComponent GetItem(int x, int y) => grid == null ? null : grid.Get(x, y);
    public bool CanFit(ItemComponent item, Vector2Int position, Vector2Int[] occupiedOffsets)
    {
        if (grid == null || occupiedOffsets == null) return false;
        var shape = new GridCell[occupiedOffsets.Length];
        for (int i = 0; i < shape.Length; i++) shape[i] = new GridCell(occupiedOffsets[i].x, occupiedOffsets[i].y);
        return grid.CanPlace(item, shape, position.x, position.y, 0);
    }
    public bool TryPlace(ItemComponent item, Vector2Int position, int rotation)
    {
        if (grid == null || item == null || item.Definition == null || (item.Container != null && item.Container != this)) return false;
        if (!grid.TryPlace(item, item.Definition.GetFootprint(), position.x, position.y, rotation)) return false;
        item.Container = this; item.GridPosition = position; item.SetRotation(rotation); Changed?.Invoke(); return true;
    }
    public bool TryAutoPlace(ItemComponent item)
    {
        if (grid == null || item == null || item.Definition == null || (item.Container != null && item.Container != this)) return false;
        int x, y, rotation;
        if (!grid.TryAutoPlace(item, item.Definition.GetFootprint(), out x, out y, out rotation)) return false;
        item.Container = this; item.GridPosition = new Vector2Int(x, y); item.SetRotation(rotation); Changed?.Invoke(); return true;
    }
    public bool Remove(ItemComponent item)
    {
        if (grid == null || item == null || item.Container != this || !grid.Remove(item)) return false;
        item.Container = null; Changed?.Invoke(); return true;
    }
}
