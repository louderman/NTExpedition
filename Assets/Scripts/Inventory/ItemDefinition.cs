
using UnityEngine;

// Describes a KIND of item. UI reads this asset; this asset does not control UI.
// Identical item instances may all reference this same ScriptableObject asset.
[CreateAssetMenu(menuName = "NTExpedition/Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    // TODO: Add Inspector-editable fields for display name and icon.
    // Relevant types: string and Sprite.
    [SerializeField] string itemName;
    [SerializeField] Sprite itemIcon;

    // TODO: Add the unrotated grid footprint, measured in whole cells.
    // Relevant type: Vector2Int. Both dimensions must be positive.
    [SerializeField] Vector2Int itemDimensions;
    public Vector2Int ItemDimensions => itemDimensions;

    // TODO: Add empty item weight, in kilograms.
    // Relevant type: float. Loaded contents will contribute separately later.
    [SerializeField] float itemWeight;

    // Do NOT put current grid position, current rotation, remaining ammunition,
    // or the inserted magazine here: those differ between individual instances.
    // Magazine capacity is shared configuration too, but will belong to a
    // magazine-specific definition when we build that feature, not every item.
}
