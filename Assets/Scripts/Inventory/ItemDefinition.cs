using UnityEngine;

[CreateAssetMenu(menuName = "NTExpedition/Items/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [SerializeField] string itemName;
    [SerializeField] Sprite itemIcon;
    [SerializeField] Vector2Int itemDimensions = Vector2Int.one;
    [SerializeField] Vector2Int[] occupiedCells;
    [SerializeField] float itemWeight;
    [Header("Melee weapon (optional)")]
    [SerializeField] bool isMeleeWeapon;
    [SerializeField, Min(0f)] float meleeDamage = 18f;
    [SerializeField, Min(0.1f)] float meleeRange = 1.5f;
    [SerializeField, Min(0.05f)] float meleeCooldown = 0.55f;
    [SerializeField, Range(10f, 220f)] float meleeSwingArc = 120f;
    [SerializeField, Min(0.05f)] float meleeSwingDuration = 0.24f;
    [SerializeField, Min(0f)] float meleeKnockback = 0.8f;
    [SerializeField] float meleeSpriteAngle = 90f;
    public string ItemName => itemName;
    public Sprite ItemIcon => itemIcon;
    public Vector2Int ItemDimensions => itemDimensions;
    public float ItemWeight => itemWeight;
    public bool IsMeleeWeapon => isMeleeWeapon;
    public float MeleeDamage => meleeDamage;
    public float MeleeRange => meleeRange;
    public float MeleeCooldown => meleeCooldown;
    public float MeleeSwingArc => meleeSwingArc;
    public float MeleeSwingDuration => meleeSwingDuration;
    public float MeleeKnockback => meleeKnockback;
    public float MeleeSpriteAngle => meleeSpriteAngle;

    // Optional explicit footprint allows L/corner shapes; empty means a rectangle.
    public GridCell[] GetFootprint()
    {
        if (occupiedCells != null && occupiedCells.Length > 0)
        {
            var shape = new GridCell[occupiedCells.Length];
            for (int i = 0; i < shape.Length; i++) shape[i] = new GridCell(occupiedCells[i].x, occupiedCells[i].y);
            return shape;
        }
        if (itemDimensions.x <= 0 || itemDimensions.y <= 0) return new GridCell[0];
        var cells = new GridCell[itemDimensions.x * itemDimensions.y];
        for (int y = 0; y < itemDimensions.y; y++)
            for (int x = 0; x < itemDimensions.x; x++) cells[y * itemDimensions.x + x] = new GridCell(x, y);
        return cells;
    }
}
