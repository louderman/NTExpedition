using UnityEngine;
using UnityEngine.InputSystem;

// SS14-style pulling: Ctrl + left-click takes hold of a world item without putting it in a hand, so both
// hands stay free while the item is dragged around. The item keeps trailing the player on a leash, and the
// right mouse button reels it in toward the cursor, clamped to that same leash radius.
//
// Nothing is reparented, stored or deactivated while a drag is running: the item stays an ordinary world
// ItemComponent the whole time, which is what lets the hand system, the throw system and the portal carry
// keep treating it normally. Dropping only forgets the reference - the item is already where it should be.
public class ItemDragSystem : MonoBehaviour
{
    [SerializeField] private float grabRange = 2f;        // how far a Ctrl+click reaches, measured like HandSystem.IsInRange
    [SerializeField] private float maxDragRadius = 2.5f;  // leash: the dragged item is never left further out than this
    [SerializeField] private float followSpeed = 8f;      // units/second the item is reeled in once it hits the leash
    [SerializeField] private float pullSpeed = 7f;        // units/second while the right button pulls it toward the cursor

    private HandSystem hands;
    private PlayerMovement movement;
    private Rigidbody2D body;
    private ItemComponent dragged;
    private bool pulling;

    public ItemComponent DraggedItem => dragged;
    public bool IsDragging => dragged != null;
    public float MaxDragRadius => maxDragRadius;

    void Awake()
    {
        hands = GetComponent<HandSystem>();
        movement = GetComponent<PlayerMovement>();
        body = GetComponent<Rigidbody2D>();
    }

    // Ctrl is the modifier that turns a click into a drag, so letting go of Ctrl has to drop the item even
    // though no input action fires for the modifier itself. Polled per frame, like the other keyboard
    // checks in this project (PlayerMovement.ProcessFacing, ScenePortal.Update).
    static bool CtrlHeld => Keyboard.current != null &&
        (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);

    void Update()
    {
        if (dragged == null) { pulling = false; return; }   // Unity's null check also covers a destroyed item
        if (Time.timeScale == 0f) return;
        if (!CtrlHeld || !CanKeepDragging(dragged)) { Drop(); return; }

        Vector2 player = transform.position;
        Vector2 item = dragged.transform.position;
        Vector2 target = item;

        if (pulling && TryCursorWorld(out Vector2 cursor))
        {
            // Pull toward the cursor, but never past the leash: the clamp is what keeps a dragged item
            // within a limited range of the player.
            target = player + Vector2.ClampMagnitude(cursor - player, maxDragRadius);
        }
        else if (Vector2.Distance(item, player) > maxDragRadius)
        {
            // Idle drag: the item stays where it was until the player walks far enough to pull the leash.
            target = player + (item - player).normalized * maxDragRadius;
        }

        Vector2 next = Vector2.MoveTowards(item, target, (pulling ? pullSpeed : followSpeed) * Time.deltaTime);
        if (next == item) return;
        dragged.transform.position = Sweep(item, next);
    }

    // Scene unload / disable: forget the drag silently, the item belongs to the outgoing scene.
    void OnDisable() { dragged = null; pulling = false; }

    /// <summary>
    /// Ctrl + left-click entry point. Grabs the item under the cursor, or failing that the nearest grabbable
    /// item within grabRange, so a click just off a small sprite still works.
    /// </summary>
    public bool TryGrab(Vector2 screenPosition)
    {
        if (Time.timeScale == 0f || dragged != null || !CtrlHeld) return false;
        var item = FindGrabTarget(screenPosition != Vector2.zero ? screenPosition : CursorScreen());
        return item != null && BeginDrag(item);
    }

    /// <summary>Starts dragging an already chosen item (also used when a portal restores a dragged item).</summary>
    public bool BeginDrag(ItemComponent item)
    {
        if (!CanGrab(item)) return false;
        dragged = item;
        pulling = false;
        Debug.Log($"[Drag] grabbed {item.name} ({item.Definition.name})");
        return true;
    }

    /// <summary>Lets go where the item is. The item itself is untouched.</summary>
    public void Drop()
    {
        if (dragged == null) { pulling = false; return; }
        Debug.Log($"[Drag] dropped {dragged.name} at {dragged.transform.position}");
        dragged = null;
        pulling = false;
    }

    /// <summary>Right mouse button held = pull toward the cursor; released = stop pulling, keep dragging.</summary>
    public void SetPull(bool value)
    {
        if (dragged != null) pulling = value;
    }

    public bool CanGrab(ItemComponent item)
    {
        if (item == null || item.Definition == null) return false;
        if (!item.gameObject.activeInHierarchy || item.Container != null) return false;
        if (IsHeldInHand(item)) return false;
        // Combat mode is a hands-busy state in this project (HandSystem.TryPickup refuses too), so an item
        // cannot be taken hold of while fighting; ActionManager also drops an existing drag on the toggle.
        if (movement != null && movement.combatMode) return false;
        var flight = item.GetComponent<ItemThrow>();
        if (flight != null && flight.IsMoving) return false;
        return IsInReach(item);
    }

    bool CanKeepDragging(ItemComponent item)
    {
        if (!item.gameObject.activeInHierarchy || item.Container != null) return false;
        if (IsHeldInHand(item)) return false;     // picked up mid-drag: the hands win
        if (movement != null && movement.combatMode) return false;
        return true;
    }

    bool IsHeldInHand(ItemComponent item)
    {
        if (hands == null) return false;
        return hands.HeldItem(HandSystem.HandSlot.Left) == item || hands.HeldItem(HandSystem.HandSlot.Right) == item;
    }

    // Same measurement the hand system uses: reach is collider-to-player, so grabRange means the same thing
    // whether the item is picked up or dragged.
    bool IsInReach(ItemComponent item)
    {
        var collider = item.GetComponent<Collider2D>();
        return collider != null && collider.enabled &&
            Vector2.Distance(transform.position, collider.ClosestPoint(transform.position)) <= grabRange;
    }

    ItemComponent FindGrabTarget(Vector2 screenPosition)
    {
        Vector2 world = ToWorld(screenPosition);
        ItemComponent underCursor = null;
        int bestOrder = int.MinValue;
        ItemComponent nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (var item in FindObjectsByType<ItemComponent>(FindObjectsSortMode.None))
        {
            if (!CanGrab(item)) continue;
            var collider = item.GetComponent<Collider2D>();
            if (collider != null && collider.OverlapPoint(world))
            {
                var renderer = item.GetComponent<SpriteRenderer>();
                int order = renderer == null ? 0 : renderer.sortingOrder;
                if (underCursor == null || order > bestOrder) { underCursor = item; bestOrder = order; }
            }
            float distance = Vector2.Distance(transform.position, item.transform.position);
            if (distance < nearestDistance) { nearest = item; nearestDistance = distance; }
        }
        return underCursor != null ? underCursor : nearest;
    }

    Vector2 CursorScreen()
    {
        if (Mouse.current != null) return Mouse.current.position.ReadValue();
        return Vector2.zero;
    }

    bool TryCursorWorld(out Vector2 world)
    {
        world = ToWorld(CursorScreen());
        return Camera.main != null;
    }

    Vector2 ToWorld(Vector2 screenPosition)
    {
        if (Camera.main == null) return transform.position;
        return Camera.main.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, -Camera.main.transform.position.z));
    }

    // Same collision rule as ItemThrow: a solid scene collider stops the item instead of letting it be
    // dragged through a wall. Triggers, the dragged item itself and the player are ignored.
    Vector2 Sweep(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0f) return to;
        Vector2 direction = delta / distance;
        float allowed = distance;
        foreach (var hit in Physics2D.CircleCastAll(from, Radius(dragged), direction, distance))
        {
            var collider = hit.collider;
            if (collider == null || collider.isTrigger) continue;
            if (collider.transform.IsChildOf(dragged.transform) || collider.transform.IsChildOf(transform)) continue;
            if (body != null && collider.attachedRigidbody == body) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - .01f));
        }
        return from + direction * allowed;
    }

    static float Radius(ItemComponent item)
    {
        var collider = item.GetComponent<Collider2D>();
        if (collider is CircleCollider2D circle)
            return circle.radius * Mathf.Max(Mathf.Abs(item.transform.lossyScale.x), Mathf.Abs(item.transform.lossyScale.y));
        if (collider != null)
        {
            Vector2 size = collider.bounds.size * .5f;
            return Mathf.Max(.05f, Mathf.Min(size.x, size.y));
        }
        return .1f;
    }
}
