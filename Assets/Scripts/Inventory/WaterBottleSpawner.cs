using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public class WaterBottleSpawner : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Spawn(SceneManager.GetActiveScene());
    }
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Spawn(scene); }
    static void Spawn(Scene scene)
    {
        if (scene.name != "HomeShip") return;
        var player = UnityEngine.Object.FindFirstObjectByType<HandSystem>();
        var prefab = Resources.Load<GameObject>("WorldItems/WaterBottle");
        if (player == null || prefab == null) { Debug.LogError("Water bottle: player or prefab missing"); return; }
        var bottle = Instantiate(prefab, player.transform.position + new Vector3(0.8f, 0f, 0f), Quaternion.identity);
        var component = bottle.GetComponent<ItemComponent>();
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--pickup-self-test") >= 0)
            player.gameObject.AddComponent<WaterBottlePickupTest>().Configure(player, component);
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--release-self-test") >= 0)
            player.gameObject.AddComponent<WaterBottleReleaseTest>().Configure(player, component);
    }
}

public class WaterBottlePickupTest : MonoBehaviour
{
    HandSystem hands;
    ItemComponent bottle;
    public void Configure(HandSystem h, ItemComponent b) { hands = h; bottle = b; }
    IEnumerator Start()
    {
        foreach (var device in InputSystem.devices)
            if (device is Mouse && device.native) InputSystem.DisableDevice(device);
        var mouse = InputSystem.AddDevice<Mouse>();
        yield return null;
        var position = (Vector2)Camera.main.WorldToScreenPoint(bottle.transform.position);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        yield return null; yield return null;
        bool nearHover = hands.HoveredItem == bottle && hands.HoverInRange;
        var outline = bottle.transform.Find("Pickup outline");
        nearHover &= outline != null && outline.GetComponentInChildren<SpriteRenderer>() != null && outline.GetComponentInChildren<SpriteRenderer>().color == Color.green;
        Vector3 playerStart = hands.transform.position;
        hands.transform.position = playerStart + new Vector3(3f, 0f, 0f);
        Physics2D.SyncTransforms();
        yield return null;
        position = (Vector2)Camera.main.WorldToScreenPoint(bottle.transform.position);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        yield return null; yield return null;
        bool farHover = hands.HoveredItem == bottle && !hands.HoverInRange;
        outline = bottle.transform.Find("Pickup outline");
        farHover &= outline != null && outline.GetComponentInChildren<SpriteRenderer>().color == Color.red;
        hands.transform.position = playerStart;
        Physics2D.SyncTransforms();
        yield return null;
        position = (Vector2)Camera.main.WorldToScreenPoint(bottle.transform.position);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
        yield return null; yield return null;
        bool ok = nearHover && farHover && hands.HeldItem(hands.CurrentHandSlot) == bottle && !bottle.gameObject.activeSelf;
        var ui = UnityEngine.Object.FindFirstObjectByType<HandUI>();
        var firstIcon = hands.CurrentHandSlot == HandSystem.HandSlot.Left ? ui.LeftItemIcon : ui.RightItemIcon;
        ok &= firstIcon != null && firstIcon.enabled && firstIcon.sprite == bottle.Definition.ItemIcon;
        Debug.Log($"PICKUP_PROBE near={nearHover} far={farHover} first={firstIcon != null && firstIcon.enabled}");
        var prefab = Resources.Load<GameObject>("WorldItems/WaterBottle");
        var second = Instantiate(prefab, hands.transform.position + Vector3.right * .8f, Quaternion.identity).GetComponent<ItemComponent>();
        ok &= !hands.TryPickup(second) && second.gameObject.activeSelf;
        hands.SwapHands();
        var movement = hands.GetComponent<PlayerMovement>();
        movement.combatMode = true;
        Physics2D.SyncTransforms();
        position = (Vector2)Camera.main.WorldToScreenPoint(second.transform.position);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
        yield return null; yield return null;
        bool blockedCombat = second.gameObject.activeSelf && hands.HeldItem(hands.CurrentHandSlot) == null && !hands.TryPickup(second);
        ok &= blockedCombat;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        yield return null;
        movement.combatMode = false;
        second.transform.position = hands.transform.position + Vector3.right * 10f;
        Physics2D.SyncTransforms();
        ok &= !hands.TryPickup(second) && second.gameObject.activeSelf;
        second.transform.position = hands.transform.position + Vector3.right * .8f;
        Physics2D.SyncTransforms();
        yield return null;
        position = (Vector2)Camera.main.WorldToScreenPoint(second.transform.position);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
        yield return null; yield return null;
        ok &= hands.HeldItem(hands.CurrentHandSlot) == second && !second.gameObject.activeSelf;
        var secondIcon = hands.CurrentHandSlot == HandSystem.HandSlot.Left ? ui.LeftItemIcon : ui.RightItemIcon;
        ok &= secondIcon != null && secondIcon.enabled && secondIcon.sprite == second.Definition.ItemIcon;
        ok &= hands.HeldItem(HandSystem.HandSlot.Left) != hands.HeldItem(HandSystem.HandSlot.Right);
        Debug.Log($"PICKUP_PROBE combatBlocked={blockedCombat} secondIcon={secondIcon != null && secondIcon.enabled}");
        Debug.Log(ok ? "WATER_PICKUP_INPUT_PASS" : "WATER_PICKUP_INPUT_FAIL");
        Application.Quit(ok ? 0 : 1);
    }
}
