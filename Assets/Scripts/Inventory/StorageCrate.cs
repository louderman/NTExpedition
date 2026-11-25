using UnityEngine;
using UnityEngine.InputSystem;

// The crate is both an interaction target (click to open the storage UI) and a world fixture the drag
// system can pull around: it implements IDragTarget, which grants the drag leash and nothing else. It is
// deliberately NOT an ItemComponent, so no hand, container, throw or portal-carry path can ever see it.
[RequireComponent(typeof(Collider2D))]
public class StorageCrate : MonoBehaviour, IDragTarget
{
    [SerializeField] private InventorySandbox inventoryUI;
    [SerializeField] private PlayerMovement player;
    [SerializeField] private float interactionDistance = 2f;

    // Frontier/SS14 draws a crate as two layers on one entity: the fixed `base` body (on this
    // GameObject) plus the `Door` layer, which is the lid. That lid swaps state between `closed`
    // and `open` (EntityStorageVisuals: stateDoorClosed/stateDoorOpen). Only `base` was ported, so
    // the crate rendered without its lid; the lid layer now lives on the CrateLid child.
    [Header("Lid layer (SS14 crate door)")]
    [SerializeField] private SpriteRenderer lidRenderer;
    [SerializeField] private Sprite closedLidSprite;
    [SerializeField] private Sprite openLidSprite;

    private Collider2D crateCollider;
    private SpriteRenderer bodyRenderer;
    private bool isOpen;

    // ---------------------------------------------------------------- IDragTarget (world-only dragging)
    public Transform DragTransform => transform;
    public Collider2D DragCollider => crateCollider;
    public string DragLabel => name;

    // A fixture that is switched off, or whose collider is gone, cannot be taken hold of. Being draggable
    // never implies being pickable: this is the drag system's eligibility check, not an item one.
    public bool CanStartDrag => isActiveAndEnabled && crateCollider != null && crateCollider.enabled;
    public bool CanKeepDrag => CanStartDrag;

    // The drag indicator the hand HUD shows while this crate occupies a hand is the crate's OWN authored
    // body sprite - the SS14 `base` layer drawn on this GameObject - so the icon is the same art that is on
    // screen in the world and can never drift from it. No new asset, no scene reference, no scene edit.
    public Sprite DragIcon => bodyRenderer != null ? bodyRenderer.sprite : null;

    void OnEnable() { ItemDragSystem.RegisterFixture(this); }

    void OnDisable() { ItemDragSystem.UnregisterFixture(this); }

    void Awake()
    {
        crateCollider = GetComponent<Collider2D>();
        bodyRenderer = GetComponent<SpriteRenderer>();

        if (inventoryUI == null)
            inventoryUI = GetComponent<InventorySandbox>();

        // Only show inventory UI when the crate is open.
        if (inventoryUI != null)
            inventoryUI.enabled = false;

        SetLidOpen(false);
    }

    void Update()
    {
        // The crate is part of the persistent travelling ship (HomeShipStaging), so after a scene hop its
        // authored `player` reference can point at the player of the scene that was unloaded. Re-resolve it
        // instead of letting the crate go inert in the arriving scene.
        if (player == null) player = FindFirstObjectByType<PlayerMovement>();

        if (inventoryUI == null || player == null)
            return;

        bool inRange = Vector2.Distance(
            player.transform.position,
            crateCollider.ClosestPoint(player.transform.position)
        ) <= interactionDistance;

        // Close the crate if the player moves too far away.
        if (isOpen && (!inRange || player.combatMode))
            SetOpen(false);

        if (!inRange || player.combatMode || Time.timeScale == 0f)
            return;

        // Ctrl + left click is the drag modifier: with Ctrl held the click means "drag this crate", so the
        // open/close interaction has to stay out of it. Without this the same click opened the crate AND
        // started a drag on whatever the drag system found nearby.
        if (CtrlHeld)
            return;

        if (Mouse.current == null || Camera.main == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
            return;
        if (ExpeditionMenu.PointerOverInterface()) return;

        Vector2 mouseWorld = Camera.main.ScreenToWorldPoint(
            Mouse.current.position.ReadValue()
        );

        // Pressing the crate opens or closes it
        if (crateCollider.OverlapPoint(mouseWorld))
            SetOpen(!isOpen);
    }

    void SetOpen(bool open)
    {
        isOpen = open;
        inventoryUI.enabled = open;
        SetLidOpen(open);
    }

    // SS14 `EntityStorageVisuals`: the door layer shows `closed` while shut and `open` while open.
    void SetLidOpen(bool open)
    {
        if (lidRenderer == null || closedLidSprite == null || openLidSprite == null)
            return;

        lidRenderer.sprite = open ? openLidSprite : closedLidSprite;
        lidRenderer.enabled = true;
    }

    // Same modifier poll as ItemDragSystem.CtrlHeld: Ctrl has no input action of its own in this project.
    static bool CtrlHeld => Keyboard.current != null &&
        (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
}
