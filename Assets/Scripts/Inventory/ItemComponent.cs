using UnityEngine;

// Per-instance state. Definitions remain shared and immutable during play.
public class ItemComponent : MonoBehaviour
{
    [SerializeField] private ItemDefinition definition;
    public ItemDefinition Definition => definition;
    public void Initialize(ItemDefinition itemDefinition) { if (Container == null) definition = itemDefinition; }
    public int Rotation { get; private set; }
    public bool IsRotated => Rotation % 2 != 0;
    public InventoryContainer Container { get; internal set; }
    public Vector2Int GridPosition { get; internal set; }

    // Rotating a stored item must go through its container for collision checks.
    public void Rotate()
    {
        int next = (Rotation + 1) % 4;
        if (Container != null) Container.TryPlace(this, GridPosition, next);
        else Rotation = next;
    }
    internal void SetRotation(int rotation) { Rotation = ((rotation % 4) + 4) % 4; }
    public Vector2Int ReturnDimensions()
    {
        if (Definition == null) return Vector2Int.zero;
        GridCell[] shape = InventoryGrid<ItemComponent>.Rotate(Definition.GetFootprint(), Rotation);
        int width = 0, height = 0;
        foreach (var cell in shape) { width = Mathf.Max(width, cell.X + 1); height = Mathf.Max(height, cell.Y + 1); }
        return new Vector2Int(width, height);
    }
}
