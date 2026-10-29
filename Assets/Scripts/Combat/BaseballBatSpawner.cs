using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public class BaseballBatSpawner : MonoBehaviour
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
        if (scene.name != "HomeShip" || GameObject.Find("BaseballBat") != null) return;
        var hands = UnityEngine.Object.FindFirstObjectByType<HandSystem>();
        var prefab = Resources.Load<GameObject>("WorldItems/BaseballBat");
        if (hands == null || prefab == null)
        {
            Debug.LogError("Baseball bat: player or prefab missing");
            return;
        }

        var bat = Instantiate(prefab, hands.transform.position + Vector3.left * .8f, Quaternion.identity);
        bat.name = "BaseballBat";
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--baseball-bat-self-test") >= 0)
            hands.gameObject.AddComponent<BaseballBatCombatTest>().Configure(hands, bat.GetComponent<ItemComponent>());
    }
}

// Opt-in executable test: actual hand state, PlayerInput actions, physics damage, cooldown and captures.
public class BaseballBatCombatTest : MonoBehaviour
{
    HandSystem hands;
    ItemComponent bat;
    Mouse mouse;
    Keyboard keyboard;
    bool ok = true;
    string evidenceDirectory;

    public void Configure(HandSystem handSystem, ItemComponent batItem)
    {
        hands = handSystem;
        bat = batItem;
    }

    IEnumerator Start()
    {
        evidenceDirectory = Environment.GetEnvironmentVariable("NTEXPEDITION_EVIDENCE_DIR");
        foreach (var device in InputSystem.devices)
            if ((device is Keyboard || device is Mouse) && device.native) InputSystem.DisableDevice(device);
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        yield return null;

        var weapon = hands.GetComponent<WeaponController>();
        var movement = hands.GetComponent<PlayerMovement>();
        Check(bat != null && bat.Definition != null && bat.Definition.IsMeleeWeapon, "bat definition is a melee weapon");
        var icon = bat != null && bat.Definition != null ? bat.Definition.ItemIcon : null;
        Check(icon != null && icon.name == "baseball-bat" && Approximately(icon.pixelsPerUnit, 32f),
            "bat uses the imported Frontier sprite at native pixel scale");
        Check(hands.TryPickup(bat), "baseball bat picks up into active hand");
        Check(hands.HeldItem(hands.CurrentHandSlot) == bat && !bat.gameObject.activeSelf,
            "picked-up bat uses existing held-item state");

        yield return PressKey(Key.Digit1);
        Check(movement.combatMode, "combat mode toggles through PlayerInput");

        Vector2 player = hands.transform.position;
        var rightTarget = MakeTarget("bat direction target", player + Vector2.right * 1.05f, true);
        var leftTarget = MakeTarget("opposite-side target", player + Vector2.left * 1.05f, false);
        yield return PointAt(rightTarget.transform.position);
        yield return TapPrimary();
        Check(weapon.IsSwinging && weapon.LastAttackDirection.x > .9f,
            "primary attack starts a visible swing toward cursor");
        yield return TapPrimary();
        yield return Capture("baseball-bat-swing-start.png", .025f);
        yield return Capture("baseball-bat-swing-middle.png", .07f);
        yield return Capture("baseball-bat-swing-end.png", .07f);
        yield return new WaitForSecondsRealtime(.12f);
        Check(CaptureExists("baseball-bat-swing-start.png")
            && CaptureExists("baseball-bat-swing-middle.png")
            && CaptureExists("baseball-bat-swing-end.png"),
            "captured three distinct swing phases");
        Check(!weapon.IsSwinging && GameObject.Find("Active melee swing") == null,
            "swing visual clears after animation");
        Check(Approximately(rightTarget.GetComponent<Health>().CurrentHP, 82f)
            && Approximately(leftTarget.GetComponent<Health>().CurrentHP, 100f),
            "rightward swing damages only target in aim direction once despite duplicate colliders");
        Check(Approximately(rightTarget.GetComponent<Health>().CurrentHP, 82f),
            "cooldown blocks immediate repeated attack");
        yield return new WaitForSecondsRealtime(.15f);
        yield return TapPrimary();
        yield return new WaitForSecondsRealtime(.2f);
        Check(Approximately(rightTarget.GetComponent<Health>().CurrentHP, 64f),
            "attack works again after cooldown and still applies one hit");

        yield return PressKey(Key.X);
        Check(hands.HeldItem(hands.CurrentHandSlot) == null && !weapon.HasActiveMeleeWeapon,
            "switching to empty hand removes active melee weapon");
        var unarmedTarget = MakeTarget("unarmed fallback target", player + Vector2.up * .75f, false);
        yield return PointAt(unarmedTarget.transform.position);
        yield return TapPrimary();
        yield return new WaitForSecondsRealtime(.25f);
        Check(Approximately(unarmedTarget.GetComponent<Health>().CurrentHP, 90f),
            "empty active hand falls back to existing unarmed damage");

        yield return new WaitForSecondsRealtime(.85f);
        yield return PressKey(Key.X);
        Check(hands.HeldItem(hands.CurrentHandSlot) == bat && weapon.HasActiveMeleeWeapon,
            "switching back restores held bat without duplication");

        Debug.Log(ok ? "BASEBALL_BAT_COMBAT_PASS" : "BASEBALL_BAT_COMBAT_FAIL");
        Application.Quit(ok ? 0 : 1);
    }

    GameObject MakeTarget(string name, Vector2 position, bool duplicateCollider)
    {
        var target = new GameObject(name);
        target.SetActive(false);
        target.transform.position = position;
        var body = target.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var box = target.AddComponent<BoxCollider2D>();
        box.size = new Vector2(.35f, .35f);
        if (duplicateCollider)
        {
            var circle = target.AddComponent<CircleCollider2D>();
            circle.radius = .16f;
        }
        var health = target.AddComponent<Health>();
        JsonUtility.FromJsonOverwrite("{\"maxHP\":100}", health);
        target.SetActive(true);
        Physics2D.SyncTransforms();
        return target;
    }

    IEnumerator PressKey(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }

    IEnumerator PointAt(Vector3 worldPosition)
    {
        var screen = (Vector2)Camera.main.WorldToScreenPoint(worldPosition);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
        yield return null;
        yield return null;
    }

    IEnumerator TapPrimary()
    {
        Vector2 position = mouse.position.ReadValue();
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        yield return null;
    }

    IEnumerator Capture(string fileName, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (string.IsNullOrEmpty(evidenceDirectory)) yield break;
        Directory.CreateDirectory(evidenceDirectory);
        string path = Path.Combine(evidenceDirectory, fileName);
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForEndOfFrame();
    }

    bool CaptureExists(string fileName)
    {
        return !string.IsNullOrEmpty(evidenceDirectory) && File.Exists(Path.Combine(evidenceDirectory, fileName));
    }

    static bool Approximately(float actual, float expected) { return Mathf.Abs(actual - expected) < .01f; }

    void Check(bool result, string name)
    {
        Debug.Log((result ? "PASS: " : "FAIL: ") + name);
        ok &= result;
    }
}
