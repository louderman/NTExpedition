using UnityEngine;

// A gun and a bottle can both have this component; weapon behavior stays separate.
public class ItemComponent : MonoBehaviour
{
    // Assign the shared asset in this item's Inspector, not another scene item.
    [SerializeField] private ItemDefinition definition;

    // A read-only property exposes the referenced asset to other components.
    // Each access returns the reference currently held in the field above.
    public ItemDefinition Definition => definition;

    // TODO: Add per-instance state recording whether its footprint is rotated.
    // Relevant type: bool. Do not rotate the shared definition's footprint.
    private bool isRotated = false;
    public bool IsRotated => isRotated;

    public void Rotate()
    {
        if(isRotated == false){isRotated = true;}
        else{isRotated = false;}
    }

    public Vector2Int ReturnDimensions()
    {
        if(isRotated == false){return Definition.ItemDimensions;}
        else
        {
            Vector2Int flipped = new(Definition.ItemDimensions.y, Definition.ItemDimensions.x);
            return flipped;
        }
    }
    // Later: the grid container will record placement and validate transfers.
    // Later: magazine-specific state belongs to a magazine behavior component.
    // Do not add attack, UI-rendering, or reloading methods to this component.
}
