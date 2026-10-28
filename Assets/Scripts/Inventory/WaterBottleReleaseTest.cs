using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Opt-in test that exercises the actual PlayerInput action and virtual devices.
public class WaterBottleReleaseTest : MonoBehaviour
{
    HandSystem hands; ItemComponent bottle;
    public void Configure(HandSystem h, ItemComponent b) { hands = h; bottle = b; }
    IEnumerator Start()
    {
        foreach (var device in InputSystem.devices)
            if ((device is Keyboard || device is Mouse) && device.native) InputSystem.DisableDevice(device);
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
        yield return null;
        bool ok = hands.TryPickup(bottle);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
        yield return null; yield return null;
        var ui = Object.FindFirstObjectByType<HandUI>();
        var icon = hands.CurrentHandSlot == HandSystem.HandSlot.Left ? ui.LeftItemIcon : ui.RightItemIcon;
        bool dropped = bottle.gameObject.activeSelf && hands.HeldItem(hands.CurrentHandSlot) == null && icon != null && !icon.enabled;
        ok &= dropped;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        bool reacquired = hands.TryPickup(bottle); ok &= reacquired;
        Vector3 target = hands.transform.position + Vector3.right * 5f;
        var screen = (Vector2)Camera.main.WorldToScreenPoint(target);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl, Key.Q));
        yield return null; yield return null;
        Vector3 start = bottle.transform.position;
        yield return new WaitForSecondsRealtime(.25f);
        bool thrown = bottle.gameObject.activeSelf && hands.HeldItem(hands.CurrentHandSlot) == null && bottle.transform.position.x > start.x + .1f;
        ok &= thrown;
        yield return new WaitForSecondsRealtime(.6f);
        bottle.transform.position = hands.transform.position + Vector3.right * .8f;
        Physics2D.SyncTransforms();
        ok &= hands.TryPickup(bottle);
        var playerInput = hands.GetComponent<PlayerInput>();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        playerInput.DeactivateInput();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
        yield return null; yield return null;
        bool disabledSafe = hands.HeldItem(hands.CurrentHandSlot) == bottle;
        ok &= disabledSafe;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
        playerInput.ActivateInput();
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightCtrl, Key.Q));
        yield return null; yield return null;
        var secondFlight = bottle.GetComponent<ItemThrow>();
        bool rightCtrlThrow = bottle.gameObject.activeSelf && secondFlight != null && secondFlight.IsMoving;
        ok &= rightCtrlThrow;
        var wall = new GameObject("test wall"); wall.transform.position = new Vector3(1002, 1000, 0);
        var box = wall.AddComponent<BoxCollider2D>(); box.size = new Vector2(.2f, 3f);
        var probe = new GameObject("test projectile"); probe.transform.position = new Vector3(1000, 1000, 0);
        var probeCollider = probe.AddComponent<CircleCollider2D>(); probeCollider.isTrigger = true; probeCollider.radius = .1f;
        var flight = probe.AddComponent<ItemThrow>();
        Physics2D.SyncTransforms(); flight.Launch(Vector2.right, 8f, 4f, null);
        yield return new WaitForSecondsRealtime(.7f);
        bool stoppedAtWall = !flight.IsMoving && probe.transform.position.x < 1002f && probe.transform.position.x > 1001f;
        ok &= stoppedAtWall;
        Debug.Log($"RELEASE_PROBE dropped={dropped} reacquired={reacquired} thrown={thrown} disabledSafe={disabledSafe} rightCtrl={rightCtrlThrow} wall={stoppedAtWall}");
        Debug.Log(ok ? "RELEASE_INPUT_PASS" : "RELEASE_INPUT_FAIL");
        Application.Quit(ok ? 0 : 1);
    }
}
