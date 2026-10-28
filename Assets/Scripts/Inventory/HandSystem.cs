using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class HandSystem : MonoBehaviour
{
    public enum HandSlot { Left, Right }
    public HandSlot CurrentHandSlot;
    public event System.Action<HandSlot> HandChanged;
    public event System.Action<HandSlot, ItemComponent> HeldItemChanged;
    [SerializeField] float pickupRange = 1.5f;
    [SerializeField] float throwSpeed = 8f;
    [SerializeField] float throwDistance = 4f;
    ItemComponent leftItem, rightItem, hovered;
    PlayerMovement movement;
    GameObject outlineRoot;
    SpriteRenderer[] outlineSprites;
    Material outlineMaterial;
    public ItemComponent HoveredItem => hovered;
    public bool HoverInRange { get; private set; }

    void Awake() { movement = GetComponent<PlayerMovement>(); }
    public ItemComponent HeldItem(HandSlot hand) => hand == HandSlot.Left ? leftItem : rightItem;
    bool IsInRange(ItemComponent item)
    {
        var collider = item == null ? null : item.GetComponent<Collider2D>();
        return collider != null && collider.enabled && Vector2.Distance(transform.position, collider.ClosestPoint(transform.position)) <= pickupRange;
    }
    public bool TryPickup(ItemComponent item)
    {
        if (Time.timeScale == 0 || (movement != null && movement.combatMode) || item == null || item.Definition == null || !item.gameObject.activeInHierarchy || item.Container != null || HeldItem(CurrentHandSlot) != null || !IsInRange(item)) return false;
        var flight = item.GetComponent<ItemThrow>();
        if (flight != null && flight.IsMoving) return false;
        if (CurrentHandSlot == HandSlot.Left) leftItem = item; else rightItem = item;
        ClearHover();
        item.transform.SetParent(transform, true);
        item.gameObject.SetActive(false);
        HeldItemChanged?.Invoke(CurrentHandSlot, item);
        return true;
    }
    public bool ReleaseActiveItem(bool throwing, Vector2 screenPosition)
    {
        var item = HeldItem(CurrentHandSlot);
        if (Time.timeScale == 0 || item == null) return false;
        Vector2 direction = Vector2.down;
        if (throwing && Camera.main != null)
        {
            var point = Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -Camera.main.transform.position.z));
            var delta = (Vector2)point - (Vector2)transform.position;
            if (delta.sqrMagnitude > .0001f) direction = delta.normalized;
        }
        if (CurrentHandSlot == HandSlot.Left) leftItem = null; else rightItem = null;
        item.transform.SetParent(null, true);
        item.transform.position = transform.position + (Vector3)(direction * .65f);
        item.gameObject.SetActive(true);
        HeldItemChanged?.Invoke(CurrentHandSlot, null);
        if (throwing)
        {
            var flight = item.GetComponent<ItemThrow>();
            if (flight == null) flight = item.gameObject.AddComponent<ItemThrow>();
            flight.Launch(direction, throwSpeed, throwDistance, transform);
        }
        return true;
    }
    public void SwapHands()
    {
        CurrentHandSlot = CurrentHandSlot == HandSlot.Left ? HandSlot.Right : HandSlot.Left;
        HandChanged?.Invoke(CurrentHandSlot);
        Debug.Log($"Hand changed: {CurrentHandSlot}");
    }
    void Update()
    {
        var mouse = Mouse.current;
        if (Time.timeScale == 0 || mouse == null || Camera.main == null) { ClearHover(); return; }
        Vector2 screen = mouse.position.ReadValue();
        if ((EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) || InventorySandbox.PointerOverStorage(screen)) { ClearHover(); return; }
        var world = Camera.main.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -Camera.main.transform.position.z));
        ItemComponent target = null;
        int bestOrder = int.MinValue;
        foreach (var item in FindObjectsByType<ItemComponent>(FindObjectsSortMode.None))
        {
            var collider = item.GetComponent<Collider2D>();
            if (collider == null || !collider.enabled || item.Container != null || item.Definition == null || !collider.OverlapPoint(world)) continue;
            var renderer = item.GetComponent<SpriteRenderer>();
            int order = renderer == null ? 0 : renderer.sortingOrder;
            if (target == null || order > bestOrder) { target = item; bestOrder = order; }
        }
        SetHover(target);
        if (mouse.leftButton.wasPressedThisFrame && target != null) TryPickup(target);
    }
    void SetHover(ItemComponent item)
    {
        if (item != hovered)
        {
            ClearHover(); hovered = item;
            if (item != null) CreateOutline(item);
        }
        HoverInRange = item != null && IsInRange(item);
        if (outlineSprites != null)
            foreach (var renderer in outlineSprites) if (renderer != null) renderer.color = HoverInRange ? Color.green : Color.red;
    }
    void CreateOutline(ItemComponent item)
    {
        var spriteRenderer = item.GetComponent<SpriteRenderer>();
        var shader = Resources.Load<Shader>("PickupOutline");
        if (spriteRenderer == null || spriteRenderer.sprite == null || shader == null) return;
        if (outlineMaterial == null) outlineMaterial = new Material(shader);
        outlineRoot = new GameObject("Pickup outline");
        outlineRoot.transform.SetParent(spriteRenderer.transform, false);
        outlineSprites = new SpriteRenderer[8];
        float offset = 1f / spriteRenderer.sprite.pixelsPerUnit;
        int n = 0;
        for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++)
        {
            if (x == 0 && y == 0) continue;
            var child = new GameObject("outline"); child.transform.SetParent(outlineRoot.transform, false);
            child.transform.localPosition = new Vector3(x * offset, y * offset, 0);
            var sr = child.AddComponent<SpriteRenderer>(); sr.sprite = spriteRenderer.sprite;
            sr.sharedMaterial = outlineMaterial; sr.sortingLayerID = spriteRenderer.sortingLayerID; sr.sortingOrder = spriteRenderer.sortingOrder - 1;
            sr.flipX = spriteRenderer.flipX; sr.flipY = spriteRenderer.flipY;
            outlineSprites[n++] = sr;
        }
    }
    void ClearHover()
    {
        if (outlineRoot != null) Destroy(outlineRoot);
        outlineRoot = null; outlineSprites = null; hovered = null; HoverInRange = false;
    }
    void OnDisable() { ClearHover(); }
    void OnDestroy() { if (outlineMaterial != null) Destroy(outlineMaterial); }
}
