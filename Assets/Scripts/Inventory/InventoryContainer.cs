using UnityEngine;

// A container owns the grid; item behavior and UI remain separate.
public class InventoryContainer : MonoBehaviour
{
    [SerializeField] Vector2Int containerDimensions;
    public Vector2Int ContainerDimensions => containerDimensions;

    // Each cell refers to the item occupying it, or is null when empty.
    private ItemComponent[,] cells;

    private void Awake()
    {
        // The Inspector dimensions determine the size of this container's grid.
        cells = new ItemComponent[containerDimensions.x, containerDimensions.y];
    }

    // occupiedOffsets describes the proposed shape relative to position.
    // The caller must provide the desired orientation and a valid footprint.
    public bool CanFit(ItemComponent item, Vector2Int position, Vector2Int[] occupiedOffsets)
    {
        if (item == null || cells == null || occupiedOffsets == null || occupiedOffsets.Length == 0)
            return false;

        foreach (Vector2Int offset in occupiedOffsets)
        {
            int x = position.x + offset.x;
            int y = position.y + offset.y;

            // Check bounds before indexing, or the array access could throw.
            if (x < 0 || y < 0 || x >= cells.GetLength(0) || y >= cells.GetLength(1))
                return false;

            ItemComponent occupant = cells[x, y];
            // The item's own cells are allowed during a move or rotation check.
            if (occupant != null && occupant != item)
                return false;
        }

        return true;
    }
}
