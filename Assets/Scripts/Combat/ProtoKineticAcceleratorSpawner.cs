using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

public class ProtoKineticAcceleratorSpawner : MonoBehaviour
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
        if (scene.name != "HomeShip" || GameObject.Find("ProtoKineticAccelerator") != null) return;
        var hands = UnityEngine.Object.FindFirstObjectByType<HandSystem>();
        var prefab = Resources.Load<GameObject>("WorldItems/ProtoKineticAccelerator");
        if (hands == null || prefab == null)
        {
            Debug.LogError("Proto-kinetic accelerator: player or prefab missing");
            return;
        }

        var instance = Instantiate(prefab, hands.transform.position + Vector3.up * .45f, Quaternion.identity);
        instance.name = "ProtoKineticAccelerator";
        var item = instance.GetComponent<ItemComponent>();
        if (!RunningLegacySelfTest())
        {
            Physics2D.SyncTransforms();
            if (hands.HeldItem(hands.CurrentHandSlot) == null && !hands.TryPickup(item))
                Debug.LogWarning("Proto-kinetic accelerator could not be placed in the starting hand; left beside player.");
        }
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "--pka-self-test") >= 0)
            hands.gameObject.AddComponent<ProtoKineticAcceleratorTest>().Configure(hands, item);
    }

    static bool RunningLegacySelfTest()
    {
        string[] args = Environment.GetCommandLineArgs();
        return Array.IndexOf(args, "--baseball-bat-self-test") >= 0
            || Array.IndexOf(args, "--pickup-self-test") >= 0
            || Array.IndexOf(args, "--release-self-test") >= 0
            || Array.IndexOf(args, "--inventory-transfer-self-test") >= 0;
    }
}

// Opt-in executable test covering starter state, input, aim, damage, repeat fire and hand regressions.
public class ProtoKineticAcceleratorTest : MonoBehaviour
{
    HandSystem hands;
    ItemComponent pka;
    Mouse mouse;
    Keyboard keyboard;
    bool ok = true;
    string evidenceDirectory;

    public void Configure(HandSystem handSystem, ItemComponent item)
    {
        hands = handSystem;
        pka = item;
    }

    IEnumerator Start()
    {
        evidenceDirectory = Environment.GetEnvironmentVariable("NTEXPEDITION_EVIDENCE_DIR");
        foreach (var device in InputSystem.devices)
            if ((device is Keyboard || device is Mouse) && device.native) InputSystem.DisableDevice(device);
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        yield return null;
        yield return null;

        var controller = hands.GetComponent<WeaponController>();
        var movement = hands.GetComponent<PlayerMovement>();
        var ranged = pka != null ? pka.GetComponent<RangedWeapon>() : null;
        Check(pka != null && pka.Definition != null && ranged != null,
            "starter PKA uses the existing item and active-hand architecture");
        Check(hands.HeldItem(hands.CurrentHandSlot) == pka && !pka.gameObject.activeSelf,
            "starter PKA begins in the active left hand");
        Check(ranged != null && Approximately(ranged.Damage, 5f) && Approximately(ranged.FireCooldown, .65f)
            && Approximately(ranged.Range, 8f) && Approximately(ranged.ProjectileSpeed, 12f),
            "PKA exposes tuned 5 damage, 0.65 second cooldown, 8 range and 12 speed");
        Check(ranged != null && ranged.ProjectileSprite != null
            && ranged.ProjectileSprite.texture.name.IndexOf("chronobolt", StringComparison.OrdinalIgnoreCase) >= 0
            && ranged.ProjectileSprite.texture.width == 32 && ranged.ProjectileSprite.texture.height == 32
            && Approximately(ranged.ProjectileSprite.pixelsPerUnit, 32f)
            && ranged.ProjectileSprite.texture.filterMode == FilterMode.Point
            && ranged.ProjectileSprite.texture.mipmapCount == 1,
            "PKA uses Frontier's exact chronobolt texture (32px, 32 PPU, point, no mipmaps) as its projectile");
        Check(FindWorldItem("Baseball bat") != null && FindWorldItem("Water bottle") != null,
            "starter placement preserves the existing bat and water bottle");

        Vector2 player = hands.transform.position;
        yield return PointAt(player + Vector2.left * 4f);
        Check(controller.HasActiveRangedWeapon && GameObject.Find("Held ranged weapon") != null,
            "active PKA creates a cursor-facing held visual");
        yield return Capture("pka-starter.png", .05f);

        yield return PressKey(Key.Digit1);
        Check(movement.combatMode, "combat mode toggles through PlayerInput");
        // Fire into the open room; the HomeShip's east wall is close to the spawn point.
        var target = MakeTarget("PKA damage target", player + Vector2.left * 3.4f, 50);
        yield return PointAt(target.transform.position);
        yield return TapPrimary();
        var bolt = GameObject.Find("Kinetic bolt");
        var boltRenderer = bolt != null ? bolt.GetComponent<SpriteRenderer>() : null;
        Check(ranged.ShotsFired == 1 && ranged.LastShotDirection.x < -.99f
            && boltRenderer != null && boltRenderer.sprite == ranged.ProjectileSprite
            && Mathf.Abs(Mathf.DeltaAngle(bolt.transform.eulerAngles.z, -90f)) < .1f,
            "primary fire launches one visible bolt accurately toward cursor");
        yield return Capture("pka-firing.png", .07f);
        yield return TapPrimary();
        Check(ranged.ShotsFired == 1, "fire-rate cooldown blocks an immediate duplicate shot");
        yield return new WaitForSecondsRealtime(.35f);
        Check(Approximately(target.GetComponent<Health>().CurrentHP, 45f),
            "one real Health target takes exactly 5 low damage");

        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForSecondsRealtime(.35f);
            yield return TapPrimary();
            yield return new WaitForSecondsRealtime(.35f);
        }
        Check(ranged.ShotsFired == 5 && Approximately(target.GetComponent<Health>().CurrentHP, 25f),
            "five repeated shots fire without ammo, magazine or reload state");

        yield return PressKey(Key.X);
        Check(hands.HeldItem(hands.CurrentHandSlot) == null && !controller.HasActiveRangedWeapon
            && GameObject.Find("Held ranged weapon") == null,
            "switching to the empty hand hides and deactivates the PKA");
        var unarmedTarget = MakeTarget("unarmed regression target", player + Vector2.up * .75f, 100);
        yield return PointAt(unarmedTarget.transform.position);
        yield return TapPrimary();
        yield return new WaitForSecondsRealtime(.25f);
        Check(Approximately(unarmedTarget.GetComponent<Health>().CurrentHP, 90f),
            "empty-hand primary still uses existing 10-damage unarmed attack");

        yield return PressKey(Key.Digit1);
        var bat = FindWorldItem("Baseball bat");
        Check(bat != null && hands.TryPickup(bat), "existing baseball bat still picks up into the other hand");
        yield return PressKey(Key.Digit1);
        var batTarget = MakeTarget("bat regression target", player + Vector2.left * 1.05f, 100);
        yield return PointAt(batTarget.transform.position);
        yield return TapPrimary();
        yield return new WaitForSecondsRealtime(.25f);
        Check(Approximately(batTarget.GetComponent<Health>().CurrentHP, 82f),
            "baseball bat primary still deals its existing 18 damage");

        yield return PressKey(Key.X);
        Check(hands.HeldItem(hands.CurrentHandSlot) == pka && controller.HasActiveRangedWeapon,
            "switching back restores the same starter PKA without duplication");

        Debug.Log(ok ? "PKA_COMBAT_PASS" : "PKA_COMBAT_FAIL");
        Application.Quit(ok ? 0 : 1);
    }

    ItemComponent FindWorldItem(string itemName)
    {
        foreach (var item in FindObjectsByType<ItemComponent>(FindObjectsSortMode.None))
            if (item != null && item.gameObject.activeInHierarchy && item.Definition != null
                && item.Definition.ItemName == itemName) return item;
        return null;
    }

    GameObject MakeTarget(string name, Vector2 position, int maxHealth)
    {
        var target = new GameObject(name);
        target.SetActive(false);
        target.transform.position = position;
        var body = target.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var box = target.AddComponent<BoxCollider2D>();
        box.size = new Vector2(.45f, .45f);
        var renderer = target.AddComponent<SpriteRenderer>();
        renderer.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(.5f, .5f), 1f);
        renderer.color = new Color(.5f, .08f, .08f, 1f);
        renderer.sortingOrder = 6;
        renderer.transform.localScale = new Vector3(.45f, .45f, 1f);
        var health = target.AddComponent<Health>();
        JsonUtility.FromJsonOverwrite("{\"maxHP\":" + maxHealth + "}", health);
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
        Vector2 screen = Camera.main.WorldToScreenPoint(worldPosition);
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
        yield return new WaitForSecondsRealtime(.5f);
        Check(File.Exists(path), "captured " + fileName);
    }

    static bool Approximately(float actual, float expected) { return Mathf.Abs(actual - expected) < .01f; }

    void Check(bool result, string name)
    {
        Debug.Log((result ? "PASS: " : "FAIL: ") + name);
        ok &= result;
    }
}
