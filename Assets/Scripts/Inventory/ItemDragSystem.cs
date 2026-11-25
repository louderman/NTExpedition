using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// SS14-style pulling: Ctrl + left-click takes hold of a world item without putting it in a hand, so both
// hands stay free while the item is dragged around. The item keeps trailing the player on a leash, and the
// right mouse button reels it in toward the cursor, clamped to that same leash radius.
//
// Nothing is reparented, stored or deactivated while a drag is running: the item stays an ordinary world
// ItemComponent the whole time, which is what lets the hand system, the throw system and the portal carry
// keep treating it normally. Dropping only forgets the reference - the item is already where it should be.
//
// The same drag can also take hold of a world FIXTURE (IDragTarget) that is not an item at all - the
// authored StorageCrate. Those objects get the leash, the pull and the collision sweep and nothing else:
// they are never picked up, stored, thrown or carried through a portal, because every one of those paths is
// typed on ItemComponent and a fixture deliberately has none. Drag eligibility is therefore separate from
// item pickup, not a second route into it.
//
// While a FIXTURE is held, the right button steers it toward the cursor (same cursor projection, same leash
// clamp, same movement sweep) and the hand the drag was started from is booked as occupied for the duration:
// the steer only ticks while that hand is still the selected one and the player is not in combat mode, and a
// fresh right-button press is required once either gate has closed, so an interrupted hold cannot leak
// movement. The booking is a hand-level transient - no item is created or moved - and it is released with the
// drag, on teardown, and by the existing portal release (PlayerLoadoutCarry.CaptureDragged -> Drop).
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
    private IDragTarget draggedTarget;
    private bool pulling;
    // Steering state. `pullInputHeld` is the raw right-button state; `steerArmed` records that a press
    // arrived while the steering gate was open, and `steerHand` is the hand a FIXTURE drag was started from.
    // Steering only ticks when all three line up, so an interrupted hold cannot resume on its own.
    private bool pullInputHeld;
    private bool steerArmed;
    private HandSystem.HandSlot? steerHand;

    // World fixtures that can be pulled around but are not items. They register on enable, so this list holds
    // whatever the loaded scene contains and nothing more; the crate's scene unload clears it again.
    static readonly List<IDragTarget> fixtures = new List<IDragTarget>();

    public static void RegisterFixture(IDragTarget target)
    {
        if (target != null && !fixtures.Contains(target)) fixtures.Add(target);
    }

    public static void UnregisterFixture(IDragTarget target) { fixtures.Remove(target); }

    /// <summary>Registration check, used by the crate-drag regression observer to prove the wiring.</summary>
    public static bool IsFixtureRegistered(IDragTarget target)
    {
        return target != null && fixtures.Contains(target);
    }

    public ItemComponent DraggedItem => dragged;
    public IDragTarget DraggedTarget => draggedTarget;
    public bool IsDragging => dragged != null || draggedTarget != null;
    public float MaxDragRadius => maxDragRadius;

    /// <summary>True while the right button is actually moving the drag toward the cursor (the gate is open).</summary>
    public bool IsSteering => pulling;
    /// <summary>The hand a fixture drag was started from, or null when no fixture drag has captured one.</summary>
    public HandSystem.HandSlot? SteeringHand => steerHand;

    // Name of whatever is being dragged, for the log lines and for the portal release message.
    public string DraggedLabel => dragged != null ? dragged.name
        : (draggedTarget != null ? draggedTarget.DragLabel : "nothing");

    // The current drag, whichever kind it is; the leash, the pull and the sweep are shared by both.
    Transform DraggedTransform => dragged != null ? dragged.transform
        : (draggedTarget != null ? draggedTarget.DragTransform : null);

    Collider2D DraggedCollider => dragged != null ? dragged.GetComponent<Collider2D>()
        : (draggedTarget != null ? draggedTarget.DragCollider : null);

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
        if (!IsDragging) { pulling = false; pullInputHeld = false; steerArmed = false; return; }   // Unity's null check also covers a destroyed item
        if (Time.timeScale == 0f) return;
        if (!CtrlHeld || !CanKeepDragging()) { Drop(); return; }

        // Re-checked every frame, before the move: entering combat or switching away from the hand that began
        // a fixture drag ends the steer for the rest of the hold, so no stale movement keeps leaking through.
        RefreshSteering();

        Vector2 player = transform.position;
        Vector2 item = DraggedTransform.position;
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
        DraggedTransform.position = Sweep(item, next);
    }

    // Scene unload / disable: forget the drag silently, the target belongs to the outgoing scene - and free
    // the hand booking with it, so a torn-down drag cannot leave a hand occupied.
    void OnDisable()
    {
        dragged = null;
        draggedTarget = null;
        pulling = false;
        pullInputHeld = false;
        steerArmed = false;
        steerHand = null;
        if (hands != null) hands.ClearDragReservation();
    }

    /// <summary>
    /// Ctrl + left-click entry point. Grabs whatever is under the cursor (an item or a world fixture), or
    /// failing that the nearest grabbable thing within grabRange, so a click just off a small sprite still
    /// works.
    /// </summary>
    public bool TryGrab(Vector2 screenPosition)
    {
        if (Time.timeScale == 0f || IsDragging || !CtrlHeld) return false;
        var pick = PickTarget(screenPosition != Vector2.zero ? screenPosition : CursorScreen());
        return pick.Item != null ? BeginDrag(pick.Item)
            : (pick.Target != null && BeginTargetDrag(pick.Target));
    }

    /// <summary>Starts dragging an already chosen item (also used when a portal restores a dragged item).</summary>
    public bool BeginDrag(ItemComponent item)
    {
        if (!CanGrab(item)) return false;
        dragged = item;
        draggedTarget = null;
        // An item drag keeps the original pull: no hand is captured and no hand is booked.
        steerHand = null;
        steerArmed = pullInputHeld;
        pulling = steerArmed && SteeringGateOpen();
        Debug.Log($"[Drag] grabbed {item.name} ({item.Definition.name})");
        return true;
    }

    /// <summary>
    /// Takes hold of a world fixture such as the storage crate. It is deliberately not an ItemComponent and
    /// gains no item behaviour: it is never in a hand, never in a container, never thrown, never carried
    /// through a portal. Only the leash, the pull and the collision sweep below apply to it.
    /// </summary>
    public bool BeginTargetDrag(IDragTarget target)
    {
        if (!CanGrabTarget(target)) return false;
        dragged = null;
        draggedTarget = target;
        // The hand that started the drag becomes the steering hand: it is booked as occupied for the whole
        // drag, and the steer only ticks while that same hand is still the selected one. Switching hands does
        // NOT move the booking, so the steer simply stops until the original hand is selected again.
        steerHand = hands != null ? hands.CurrentHandSlot : (HandSystem.HandSlot?)null;
        // The booking carries the fixture's own authored sprite, so the HUD can show a recognisable crate
        // icon on the occupied hand instead of text only.
        if (hands != null && steerHand.HasValue) hands.ReserveDragHand(steerHand.Value, target.DragIcon);
        steerArmed = pullInputHeld;
        pulling = steerArmed && SteeringGateOpen();
        Debug.Log($"[Drag] grabbed fixture {target.DragLabel} (world object, not an item)" +
                  (steerHand.HasValue ? $"; steering hand {steerHand.Value}" : ""));
        return true;
    }

    /// <summary>Lets go where the target is. The target itself is untouched.</summary>
    public void Drop()
    {
        if (IsDragging)
            Debug.Log($"[Drag] dropped {DraggedLabel} at {DraggedTransform.position}");
        dragged = null;
        draggedTarget = null;
        pulling = false;
        pullInputHeld = false;
        steerArmed = false;
        steerHand = null;
        // Letting go of the fixture frees the hand it occupied (a no-op for an item drag, which never booked one).
        if (hands != null) hands.ClearDragReservation();
    }

    /// <summary>
    /// Right mouse button. A press steers the drag toward the current cursor position; releasing stops the
    /// steer and keeps the drag. A press that arrives while the gate is shut is deliberately NOT remembered:
    /// the gate has to be open and a fresh press delivered, so an interrupted hold cannot resume by itself.
    /// </summary>
    public void SetPull(bool value)
    {
        pullInputHeld = value;
        if (!value) { steerArmed = false; pulling = false; return; }
        if (IsDragging && SteeringGateOpen()) { steerArmed = true; pulling = true; }
        else { steerArmed = false; pulling = false; }
    }

    // The steering gate. A fixture (crate) drag is steered only from the hand that began it, and only outside
    // combat mode - the project's hands-busy state. An item drag keeps the original ungated pull, so existing
    // loose-item dragging is unchanged.
    bool SteeringGateOpen()
    {
        if (draggedTarget == null) return true;
        if (movement != null && movement.combatMode) return false;
        if (hands == null || !steerHand.HasValue) return true;
        return hands.CurrentHandSlot == steerHand.Value;
    }

    // Called every frame and on every press/release: once the gate shuts mid-hold the steer is dropped and
    // disarmed, so re-opening the gate alone cannot restart movement.
    void RefreshSteering()
    {
        if (!pullInputHeld || !SteeringGateOpen()) { steerArmed = false; pulling = false; return; }
        pulling = steerArmed;
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
        return IsInReach(item.GetComponent<Collider2D>());
    }

    // A world fixture uses its own veto plus the same reach rule as an item, and the same hands-busy state.
    bool CanGrabTarget(IDragTarget target)
    {
        if (!Alive(target) || !target.CanStartDrag) return false;
        if (movement != null && movement.combatMode) return false;
        return IsInReach(target.DragCollider);
    }

    // Unity's fake-null lives on the UnityEngine.Object operator overload, which the compiler cannot apply
    // through an interface reference: without this cast a destroyed crate would keep answering the scan.
    static bool Alive(IDragTarget target)
    {
        var unityObject = target as Object;
        return unityObject != null;
    }

    // A drag stays valid while its target does: an item is checked with the item rules, a fixture with its
    // own. Combat mode ends both (it is the hands-busy state, exactly like HandSystem.TryPickup).
    bool CanKeepDragging()
    {
        if (dragged != null) return CanKeepDragging(dragged);
        if (draggedTarget == null) return false;
        if (movement != null && movement.combatMode) return false;
        return draggedTarget.CanKeepDrag;
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
    bool IsInReach(Collider2D collider)
    {
        return collider != null && collider.enabled &&
            Vector2.Distance(transform.position, collider.ClosestPoint(transform.position)) <= grabRange;
    }

    struct DragPick
    {
        public ItemComponent Item;   // an item under the click (normal ItemComponent drag)
        public IDragTarget Target;   // a world fixture under the click (leash only)
    }

    // One click can land inside several colliders at once (a loose bottle resting on the crate's footprint),
    // so the most precise hit wins: the smallest collider first, then whatever draws on top, then the one
    // nearest the pointer. That is what makes Ctrl+click on the crate take the CRATE instead of the loose
    // item beside it - and what keeps Ctrl+click on the loose item taking the item.
    class Hit
    {
        public Component Target;
        public Collider2D Collider;
        public bool IsItem;
        public float Area;
        public int Order;
        public float ClickDistance;    // pointer to the collider, used to break ranking ties
        public float PlayerDistance;   // player to the target, the old "click just off the sprite" fallback
    }

    DragPick PickTarget(Vector2 screenPosition)
    {
        Vector2 world = ToWorld(screenPosition);
        Hit best = null;
        Hit nearest = null;

        foreach (var item in FindObjectsByType<ItemComponent>(FindObjectsSortMode.None))
        {
            if (!CanGrab(item)) continue;
            var hit = Make(item, item.GetComponent<Collider2D>(), true, world);
            if (hit == null) continue;
            if (hit.Collider.OverlapPoint(world) && Wins(hit, best)) best = hit;
            if (nearest == null || hit.PlayerDistance < nearest.PlayerDistance) nearest = hit;
        }

        for (int i = 0; i < fixtures.Count; i++)
        {
            var fixture = fixtures[i];
            if (!CanGrabTarget(fixture)) continue;
            // IDragTarget is an interface, not a Component: everything below (renderer order, transform) needs
            // the object side of it.
            var fixtureObject = fixture as Component;
            if (fixtureObject == null) continue;
            var hit = Make(fixtureObject, fixture.DragCollider, false, world);
            if (hit == null) continue;
            if (hit.Collider.OverlapPoint(world) && Wins(hit, best)) best = hit;
            if (nearest == null || hit.PlayerDistance < nearest.PlayerDistance) nearest = hit;
        }

        var chosen = best != null ? best : nearest;
        var pick = new DragPick();
        if (chosen == null) return pick;
        if (chosen.IsItem) pick.Item = chosen.Target as ItemComponent;
        else pick.Target = chosen.Target as IDragTarget;
        return pick;
    }

    Hit Make(Component target, Collider2D collider, bool isItem, Vector2 world)
    {
        if (target == null || collider == null) return null;
        var size = collider.bounds.size;
        var renderer = target.GetComponent<SpriteRenderer>();
        return new Hit
        {
            Target = target,
            Collider = collider,
            IsItem = isItem,
            Area = size.x * size.y,
            Order = renderer == null ? 0 : renderer.sortingOrder,
            ClickDistance = Vector2.Distance(world, collider.ClosestPoint(world)),
            PlayerDistance = Vector2.Distance(transform.position, target.transform.position),
        };
    }

    static bool Wins(Hit candidate, Hit best)
    {
        if (best == null) return true;
        if (Mathf.Abs(candidate.Area - best.Area) > .0001f) return candidate.Area < best.Area;
        if (candidate.Order != best.Order) return candidate.Order > best.Order;
        return candidate.ClickDistance < best.ClickDistance;
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
    // dragged through a wall. Triggers, the dragged object itself and the player are ignored.
    Vector2 Sweep(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0f) return to;
        Vector2 direction = delta / distance;
        float allowed = distance;
        foreach (var hit in Physics2D.CircleCastAll(from, Radius(DraggedCollider), direction, distance))
        {
            var collider = hit.collider;
            if (collider == null || collider.isTrigger) continue;
            if (collider.transform.IsChildOf(DraggedTransform) || collider.transform.IsChildOf(transform)) continue;
            if (body != null && collider.attachedRigidbody == body) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - .01f));
        }
        return from + direction * allowed;
    }

    static float Radius(Collider2D collider)
    {
        if (collider is CircleCollider2D circle)
            return circle.radius * Mathf.Max(Mathf.Abs(collider.transform.lossyScale.x),
                                             Mathf.Abs(collider.transform.lossyScale.y));
        if (collider != null)
        {
            Vector2 size = collider.bounds.size * .5f;
            return Mathf.Max(.05f, Mathf.Min(size.x, size.y));
        }
        return .1f;
    }
}
