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

        // Keep the retained pickup clear of the starter PKA while remaining in pickup range.
        var bat = Instantiate(prefab,
            hands.transform.position + Vector3.left * 1.2f + Vector3.up * .7f, Quaternion.identity);
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
        CheckSwingAbovePlayer("start");
        yield return TapPrimary();
        yield return Capture("baseball-bat-swing-start.png", .025f);
        yield return Capture("baseball-bat-swing-middle.png", .07f);
        CheckSwingAbovePlayer("middle");
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
        yield return new WaitForSecondsRealtime(.3f);
        yield return TapPrimary();
        yield return new WaitForSecondsRealtime(.2f);
        Check(Approximately(rightTarget.GetComponent<Health>().CurrentHP, 64f),
            "attack works again after cooldown and still applies one hit");

        var shoveTarget = MakeShoveTarget("bat shove target", player + Vector2.up * .8f);
        yield return VerifyBatShoveDirection(weapon, player, Vector2.up, "up", "baseball-bat-shove-up",
            shoveTarget.GetComponent<Rigidbody2D>());
        yield return VerifyBatShoveDirection(weapon, player, Vector2.right, "right", "baseball-bat-shove-right", null);
        yield return VerifyBatShoveDirection(weapon, player, Vector2.down, "down", null, null);
        yield return VerifyBatShoveDirection(weapon, player, Vector2.left, "left", null, null);
        Check(CaptureExists("baseball-bat-shove-up-start.png")
            && CaptureExists("baseball-bat-shove-up-middle.png")
            && CaptureExists("baseball-bat-shove-up-recovery.png")
            && CaptureExists("baseball-bat-shove-right-start.png")
            && CaptureExists("baseball-bat-shove-right-middle.png")
            && CaptureExists("baseball-bat-shove-right-recovery.png"),
            "captured shove overlap across three phases in two representative directions");

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
        yield return PointAt(unarmedTarget.transform.position);
        yield return TapSecondary();
        Check(!weapon.IsBatShoving && GameObject.Find("Active bat shove") == null,
            "empty-hand shove does not create the bat visual");
        Check(GameObject.Find("DisarmFX(Clone)") != null,
            "empty-hand shove retains the existing particle effect");

        yield return new WaitForSecondsRealtime(.85f);
        yield return PressKey(Key.X);
        Check(hands.HeldItem(hands.CurrentHandSlot) == bat && weapon.HasActiveMeleeWeapon,
            "switching back restores held bat without duplication");

        Debug.Log(ok ? "BASEBALL_BAT_COMBAT_PASS" : "BASEBALL_BAT_COMBAT_FAIL");
        Application.Quit(ok ? 0 : 1);
    }

    IEnumerator VerifyBatShoveDirection(WeaponController weapon, Vector2 player, Vector2 direction,
        string label, string capturePrefix, Rigidbody2D shoveTargetBody)
    {
        int[] originalPlayerOrders = PlayerSortingOrders();
        yield return PointAt(player + direction * 1.2f);
        yield return TapSecondary();
        var shoveVisual = GameObject.Find("Active bat shove");
        Check(weapon.IsBatShoving && Vector2.Dot(weapon.LastAttackDirection, direction) > .9f && shoveVisual != null,
            $"bat shove starts toward {label} aim direction");
        Check(GameObject.Find("DisarmFX(Clone)") == null,
            $"bat shove toward {label} does not spawn the shove particle effect");
        if (shoveTargetBody != null)
            Check(Vector2.Dot(shoveTargetBody.linearVelocity, direction) > 0f,
                "bat shove preserves existing shove impulse semantics");
        if (direction == Vector2.up)
            Check(shoveVisual != null && Mathf.Abs(Mathf.DeltaAngle(shoveVisual.transform.localEulerAngles.z, 135f)) < 2f,
                "bat remains held sideways across the shove direction");
        CheckShoveBehindPlayer(label + " start");
        if (capturePrefix != null) yield return Capture(capturePrefix + "-start.png", 0f);

        yield return new WaitForSecondsRealtime(.045f);
        CheckShoveBehindPlayer(label + " middle");
        if (capturePrefix != null) yield return Capture(capturePrefix + "-middle.png", 0f);

        yield return new WaitForSecondsRealtime(.045f);
        CheckShoveBehindPlayer(label + " recovery");
        if (capturePrefix != null) yield return Capture(capturePrefix + "-recovery.png", 0f);

        yield return new WaitForSecondsRealtime(.18f);
        Check(!weapon.IsBatShoving && GameObject.Find("Active bat shove") == null,
            $"{label} bat shove clears its temporary visual after recovery");
        Check(PlayerSortingOrdersEqual(originalPlayerOrders),
            $"{label} bat shove leaves player sorting unchanged after recovery");
        yield return new WaitForSecondsRealtime(.75f);
    }

    void CheckShoveBehindPlayer(string phase)
    {
        var shoveVisual = GameObject.Find("Active bat shove");
        var shoveRenderer = shoveVisual != null ? shoveVisual.GetComponent<SpriteRenderer>() : null;
        bool behindEveryPlayerSprite = shoveRenderer != null;
        foreach (var playerRenderer in PlayerRenderers())
        {
            behindEveryPlayerSprite &= playerRenderer != null
                && shoveRenderer.sortingLayerID == playerRenderer.sortingLayerID
                && shoveRenderer.sortingOrder < playerRenderer.sortingOrder;
        }
        Check(behindEveryPlayerSprite, "bat shove renders behind every player sprite during " + phase);
    }

    void CheckSwingAbovePlayer(string phase)
    {
        var swingVisual = GameObject.Find("Active melee swing");
        var swingRenderer = swingVisual != null ? swingVisual.GetComponent<SpriteRenderer>() : null;
        bool aboveEveryPlayerSprite = swingRenderer != null;
        foreach (var playerRenderer in PlayerRenderers())
        {
            aboveEveryPlayerSprite &= playerRenderer != null
                && swingRenderer.sortingLayerID == playerRenderer.sortingLayerID
                && swingRenderer.sortingOrder > playerRenderer.sortingOrder;
        }
        Check(aboveEveryPlayerSprite, "normal bat swing remains above every player sprite during " + phase);
    }

    int[] PlayerSortingOrders()
    {
        var renderers = PlayerRenderers();
        var orders = new int[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) orders[i] = renderers[i] != null ? renderers[i].sortingOrder : int.MinValue;
        return orders;
    }

    bool PlayerSortingOrdersEqual(int[] expected)
    {
        var renderers = PlayerRenderers();
        if (renderers.Length != expected.Length) return false;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] == null || renderers[i].sortingOrder != expected[i]) return false;
        return true;
    }

    SpriteRenderer[] PlayerRenderers()
    {
        var suit = hands.transform.Find("Suit");
        var helmet = hands.transform.Find("Helmet");
        return new[]
        {
            suit != null ? suit.GetComponent<SpriteRenderer>() : null,
            helmet != null ? helmet.GetComponent<SpriteRenderer>() : null
        };
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

    GameObject MakeShoveTarget(string name, Vector2 position)
    {
        var target = new GameObject(name);
        target.transform.position = position;
        var body = target.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        var box = target.AddComponent<BoxCollider2D>();
        box.size = new Vector2(.35f, .35f);
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

    IEnumerator TapSecondary()
    {
        Vector2 position = mouse.position.ReadValue();
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Right));
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
