using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class StorageCrate : MonoBehaviour
{
    [SerializeField] private InventorySandbox inventoryUI;
    [SerializeField] private PlayerMovement player;
    [SerializeField] private float interactionDistance = 2f;

    private Collider2D crateCollider;
    private bool isOpen;

    void Awake()
    {
        crateCollider = GetComponent<Collider2D>();

        if (inventoryUI == null)
            inventoryUI = GetComponent<InventorySandbox>();

        // Only show inventory UI when the crate is open.
        if (inventoryUI != null)
            inventoryUI.enabled = false;
    }

    void Update()
    {
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

        if (Mouse.current == null || Camera.main == null ||
            !Mouse.current.leftButton.wasPressedThisFrame)
            return;

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
    }
}