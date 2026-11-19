using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Opt-in test for the SS14-style drag. It drives the real PlayerInput actions with virtual devices, exactly
// like WaterBottleReleaseTest, so the Ctrl+click grab and the right-button pull are exercised through the
// same path a packaged build uses. Run: <player> --drag-self-test
public class ItemDragTest : MonoBehaviour
{
    HandSystem hands;
    ItemComponent bottle;
    public void Configure(HandSystem h, ItemComponent b) { hands = h; bottle = b; }

    // Somewhere the scene has no geometry: the leash and the pull are measured here without HomeShip walls
    // in the way. The fixture is checked below and the blockers are named if it is not empty.
    static readonly Vector2 Area = new Vector2(3000f, 3000f);

    IEnumerator Start()
    {
        foreach (var device in InputSystem.devices)
            if ((device is Keyboard || device is Mouse) && device.native) InputSystem.DisableDevice(device);
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var mouse = InputSystem.AddDevice<Mouse>();
        yield return null;

        var drag = hands.GetComponent<ItemDragSystem>();
        var movement = hands.GetComponent<PlayerMovement>();
        bool ok = drag != null && movement != null;
        if (!ok)
        {
            Debug.Log("DRAG_PROBE player is missing its drag system");
            Debug.Log("ITEM_DRAG_INPUT_FAIL");
            Application.Quit(1);
            yield break;
        }

        hands.transform.position = new Vector3(Area.x, Area.y, 0f);
        Physics2D.SyncTransforms();
        yield return null; yield return null;                       // the camera follows in LateUpdate
        var prefab = Resources.Load<GameObject>("WorldItems/WaterBottle");
        var item = Object.Instantiate(prefab, hands.transform.position + Vector3.right * .8f, Quaternion.identity).GetComponent<ItemComponent>();
        Physics2D.SyncTransforms();
        yield return null;

        var blockers = new List<string>();
        foreach (var hit in Physics2D.OverlapCircleAll(Area, 4f))
            if (!hit.transform.IsChildOf(hands.transform) && !hit.transform.IsChildOf(item.transform)) blockers.Add(hit.name);
        ok &= blockers.Count == 0;
        Debug.Log($"DRAG_PROBE testAreaClear blockers={(blockers.Count == 0 ? "none" : string.Join(",", blockers))}");

        // 1. Ctrl + left click takes hold of the item without using a hand.
        var leftBefore = hands.HeldItem(HandSystem.HandSlot.Left);
        var rightBefore = hands.HeldItem(HandSystem.HandSlot.Right);
        Vector2 onItem = Camera.main.WorldToScreenPoint(item.transform.position);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
        InputSystem.QueueStateEvent(mouse, new MouseState { position = onItem });
        yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = onItem }.WithButton(MouseButton.Left));
        yield return null; yield return null;
        bool grabbed = drag.IsDragging && drag.DraggedItem == item;
        // "Not stored in a hand": the hands are exactly as they were before the grab, and the dragged item is
        // in neither of them (the player may already be holding a starting item in HomeShip).
        bool handsFree = hands.HeldItem(HandSystem.HandSlot.Left) == leftBefore &&
            hands.HeldItem(HandSystem.HandSlot.Right) == rightBefore &&
            hands.HeldItem(HandSystem.HandSlot.Left) != item && hands.HeldItem(HandSystem.HandSlot.Right) != item;
        bool inWorld = item.gameObject.activeSelf && item.Container == null;
        ok &= grabbed && handsFree && inWorld;
        Debug.Log($"DRAG_PROBE grab={grabbed} handsUnchanged={handsFree} inWorld={inWorld} handWasOccupied={leftBefore != null || rightBefore != null}");
        ScreenCapture.CaptureScreenshot(ShotPath("drag-1-grabbed.png"));
        yield return new WaitForEndOfFrame();
        InputSystem.QueueStateEvent(mouse, new MouseState { position = onItem });     // left button released
        yield return null;

        // 2. The item follows the player: left behind, it is reeled back onto the leash.
        item.transform.position = (Vector2)hands.transform.position + Vector2.right * 8f;
        Physics2D.SyncTransforms();
        yield return new WaitForSecondsRealtime(1f);
        float leash = Vector2.Distance(item.transform.position, hands.transform.position);
        bool followed = Mathf.Abs(leash - drag.MaxDragRadius) <= .05f;
        ok &= followed;
        Debug.Log($"DRAG_PROBE follow={followed} distance={leash:F2} leash={drag.MaxDragRadius:F2}");

        // 3. Right button held pulls it toward the cursor, still within the leash.
        Vector2 cursor = (Vector2)hands.transform.position + new Vector2(1.2f, 1.2f);
        Vector2 onCursor = Camera.main.WorldToScreenPoint(cursor);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = onCursor });
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = onCursor }.WithButton(MouseButton.Right));
        yield return null;
        yield return new WaitForSecondsRealtime(1f);
        float toCursor = Vector2.Distance(item.transform.position, cursor);
        bool pulled = toCursor <= .15f;
        float pulledLeash = Vector2.Distance(item.transform.position, hands.transform.position);
        ok &= pulled && pulledLeash <= drag.MaxDragRadius + .05f;
        Debug.Log($"DRAG_PROBE pull={pulled} toCursor={toCursor:F2} distance={pulledLeash:F2}");
        ScreenCapture.CaptureScreenshot(ShotPath("drag-2-pulled.png"));
        yield return new WaitForEndOfFrame();

        // 4. A cursor beyond the leash is clamped: the item stops at maxDragRadius from the player.
        InputSystem.QueueStateEvent(mouse, new MouseState { position = Camera.main.WorldToScreenPoint((Vector2)hands.transform.position + Vector2.right * 6f) }.WithButton(MouseButton.Right));
        yield return null;
        yield return new WaitForSecondsRealtime(1f);
        float clamped = Vector2.Distance(item.transform.position, hands.transform.position);
        bool clamp = Mathf.Abs(clamped - drag.MaxDragRadius) <= .1f;
        ok &= clamp;
        Debug.Log($"DRAG_PROBE clamp={clamp} distance={clamped:F2} leash={drag.MaxDragRadius:F2}");

        // 5. Releasing the right button keeps the drag; releasing Ctrl drops the item where it is.
        InputSystem.QueueStateEvent(mouse, new MouseState { position = Camera.main.WorldToScreenPoint((Vector2)hands.transform.position + Vector2.right * 6f) });
        yield return null; yield return null;
        bool kept = drag.IsDragging;
        Vector3 droppedAt = item.transform.position;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());                   // Ctrl released
        yield return null; yield return null;
        bool dropped = !drag.IsDragging && item.gameObject.activeSelf && item.Container == null;
        bool stayed = Vector2.Distance(item.transform.position, droppedAt) <= .05f;
        ok &= kept && dropped && stayed;
        Debug.Log($"DRAG_PROBE release keptDragging={kept} dropped={dropped} stayedPut={stayed}");

        // 6. Ctrl+click grabs again, a second Ctrl+click lets go (the item must not stick to the player).
        item.transform.position = hands.transform.position + Vector3.right * .8f;   // back inside grab range
        Physics2D.SyncTransforms();
        yield return null;
        Vector2 onItemAgain = Camera.main.WorldToScreenPoint(item.transform.position);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = onItemAgain }.WithButton(MouseButton.Left));
        yield return null; yield return null;
        bool regrabbed = drag.IsDragging && drag.DraggedItem == item;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = onItemAgain });
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = onItemAgain }.WithButton(MouseButton.Left));
        yield return null; yield return null;
        bool toggledOff = !drag.IsDragging && item.gameObject.activeSelf;
        ok &= regrabbed && toggledOff;
        Debug.Log($"DRAG_PROBE toggle regrabbed={regrabbed} toggledOff={toggledOff}");

        // 7. An item held in a hand is not draggable. HomeShip's player may already be holding a starting item,
        // so the case is read off the live hand instead of assuming an empty one.
        var inHand = hands.HeldItem(hands.CurrentHandSlot);
        bool pickedUpHere = false;
        if (inHand == null)
        {
            pickedUpHere = hands.TryPickup(item);
            inHand = hands.HeldItem(hands.CurrentHandSlot);
        }
        bool refusedHeld = inHand != null && !drag.CanGrab(inHand) && !drag.BeginDrag(inHand) &&
            hands.HeldItem(hands.CurrentHandSlot) == inHand;
        ok &= refusedHeld;
        Debug.Log($"DRAG_PROBE heldRefused={refusedHeld} pickedUpHere={pickedUpHere} held={inHand != null}");
        if (pickedUpHere) { hands.ReleaseActiveItem(false, Vector2.zero); yield return null; }   // back on the floor

        // 8. Combat mode refuses a new drag and lets go of a running one.
        movement.combatMode = true;
        yield return null;
        bool refusedCombat = !drag.BeginDrag(item);
        movement.combatMode = false;
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
        yield return null;
        Vector2 onFloor = Camera.main.WorldToScreenPoint(item.transform.position);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = onFloor });
        yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = onFloor }.WithButton(MouseButton.Left));
        yield return null; yield return null;
        bool grabbedBeforeCombat = drag.IsDragging;
        movement.combatMode = true;
        yield return null; yield return null;
        bool droppedForCombat = !drag.IsDragging;
        movement.combatMode = false;
        ok &= refusedCombat && grabbedBeforeCombat && droppedForCombat;
        Debug.Log($"DRAG_PROBE combat refused={refusedCombat} grabbed={grabbedBeforeCombat} released={droppedForCombat}");

        // 9. An item destroyed mid-drag is dropped, not left as a dangling reference.
        InputSystem.QueueStateEvent(mouse, new MouseState { position = onFloor });
        yield return null;
        var spare = Object.Instantiate(prefab, hands.transform.position + Vector3.right * .8f, Quaternion.identity).GetComponent<ItemComponent>();
        Physics2D.SyncTransforms();
        yield return null;
        bool spareGrabbed = drag.BeginDrag(spare);
        yield return null;
        Object.Destroy(spare.gameObject);
        yield return null; yield return null;
        bool survivedDestroy = !drag.IsDragging;
        ok &= spareGrabbed && survivedDestroy;
        Debug.Log($"DRAG_PROBE destroyed grabbed={spareGrabbed} released={survivedDestroy}");

        // 10. A portal snapshots the dragged item: CaptureDragged adds it to the carry list.
        InputSystem.QueueStateEvent(mouse, new MouseState { position = Camera.main.WorldToScreenPoint(item.transform.position) });
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = Camera.main.WorldToScreenPoint(item.transform.position) }.WithButton(MouseButton.Left));
        yield return null; yield return null;
        bool draggingForPortal = drag.IsDragging;
        PlayerLoadoutCarry.CaptureDragged(drag);
        bool captured = PlayerLoadoutCarry.PendingCarry && PlayerLoadoutCarry.PendingCarryCount == 1;
        ok &= draggingForPortal && captured;
        Debug.Log($"DRAG_PROBE portal dragging={draggingForPortal} captured={captured} pending={PlayerLoadoutCarry.PendingCarryCount}");

        Debug.Log(ok ? "ITEM_DRAG_INPUT_PASS" : "ITEM_DRAG_INPUT_FAIL");
        Application.Quit(ok ? 0 : 1);
    }

    static string ShotPath(string name)
    {
        string path = System.IO.Path.Combine(Application.persistentDataPath, name);
        Debug.Log($"DRAG_SHOT {path}");
        return path;
    }
}
