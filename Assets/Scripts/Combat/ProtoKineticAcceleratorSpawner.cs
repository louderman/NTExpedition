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
            && Approximately(ranged.Range, 8f) && Approximately(ranged.ProjectileSpeed, 12f)
            && ranged.SupportsTwoHandedUse && ranged.OneHandedSpreadDegrees > ranged.WieldedSpreadDegrees,
            "PKA exposes ranged tuning and improved two-handed accuracy");
        Check(ranged != null
            && HasAllHeldSprites(ranged, false, HandSystem.HandSlot.Left)
            && HasAllHeldSprites(ranged, false, HandSystem.HandSlot.Right)
            && HasAllHeldSprites(ranged, true, HandSystem.HandSlot.Left)
            && HasAllHeldSprites(ranged, true, HandSystem.HandSlot.Right),
            "PKA provides one-handed and wielded directional art for both hands");
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
        Vector2[] facingOffsets = { Vector2.down, Vector2.up, Vector2.right, Vector2.left };
        Key[] facingKeys = { Key.S, Key.W, Key.D, Key.A };
        var facings = (RangedWeapon.HeldDirection[])Enum.GetValues(typeof(RangedWeapon.HeldDirection));
        for (int i = 0; i < facings.Length; i++)
        {
            yield return PressKey(facingKeys[i]);
            yield return PointAt(player - facingOffsets[i] * 4f);
            Check(HeldVisualMatches(ranged, false, HandSystem.HandSlot.Left, facings[i]),
                "out-of-combat PKA follows player-facing " + facings[i] + " instead of the cursor");
        }
        player = hands.transform.position;
        Check(controller.HasActiveRangedWeapon && GameObject.Find("Held ranged weapon") != null,
            "active PKA creates a fixed directional held visual");
        yield return Capture("pka-starter.png", .05f);

        var randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(8128);
        Vector2 oneHandedDirection = ranged.ApplySpread(Vector2.left, false);
        UnityEngine.Random.InitState(8128);
        Vector2 wieldedDirection = ranged.ApplySpread(Vector2.left, true);
        UnityEngine.Random.state = randomState;
        Check(Vector2.Angle(Vector2.left, oneHandedDirection) > Vector2.Angle(Vector2.left, wieldedDirection),
            "wielded spread is deterministically tighter than one-handed spread");

        yield return PressKey(Key.Digit1);
        Check(movement.combatMode, "combat mode toggles through PlayerInput");
        yield return TapSecondary();
        Check(hands.IsTwoHandWielded(pka) && hands.IsHandReserved(HandSystem.HandSlot.Right),
            "secondary attack wields the PKA and reserves the empty offhand");
        for (int i = 0; i < facings.Length; i++)
        {
            yield return PointAt(player + facingOffsets[i] * 4f);
            Check(HeldVisualMatches(ranged, true, HandSystem.HandSlot.Left, facings[i]),
                "wielded PKA uses only the owning hand's fixed " + facings[i] + " wielded art");
        }
        Check(!hands.TryMoveActiveItemTo(HandSystem.HandSlot.Right)
            && hands.HeldItem(HandSystem.HandSlot.Left) == pka
            && hands.HeldItem(HandSystem.HandSlot.Right) == null,
            "reserved offhand rejects item transfer without duplicating the PKA");
        // Fire into the open room; the HomeShip's east wall is close to the spawn point.
        var target = MakeTarget("PKA damage target", player + Vector2.left * 3.4f, 50);
        Vector2 expectedDirection = ((Vector2)target.transform.position - (Vector2)hands.transform.position).normalized;
        yield return PointAt(target.transform.position);
        yield return TapPrimary();
        var bolt = GameObject.Find("Kinetic bolt");
        var boltRenderer = bolt != null ? bolt.GetComponent<SpriteRenderer>() : null;
        Check(ranged.ShotsFired == 1
            && Vector2.Angle(expectedDirection, ranged.LastShotDirection) <= ranged.WieldedSpreadDegrees + .01f
            && boltRenderer != null && boltRenderer.sprite == ranged.ProjectileSprite
            && Mathf.Abs(Mathf.DeltaAngle(bolt.transform.eulerAngles.z,
                Mathf.Atan2(ranged.LastShotDirection.y, ranged.LastShotDirection.x) * Mathf.Rad2Deg + 90f)) < .1f,
            "wielded primary fire launches one visible bolt within its accurate spread");
        var muzzle = (Vector2)hands.transform.position + ranged.LastShotDirection * .65f;
        var flash = GameObject.Find(MuzzleFlashEffect.ObjectName);
        var flashRenderer = flash != null ? flash.GetComponent<SpriteRenderer>() : null;
        var gunshot = GameObject.Find(FiringSoundEffect.ObjectName);
        var gunshotSource = gunshot != null ? gunshot.GetComponent<AudioSource>() : null;
        var pcmProbe = gunshot != null ? gunshot.AddComponent<FiringSoundPcmProbe>() : null;
        Check(ranged.MuzzleFlashSprite != null && ranged.MuzzleFlashSprite.texture != null
            && ranged.MuzzleFlashSprite.texture.name.Contains("pka-muzzle-bullet")
            && ranged.MuzzleFlashSprite.texture.width == 32 && ranged.MuzzleFlashSprite.texture.height == 32
            && Approximately(ranged.MuzzleFlashSprite.pixelsPerUnit, 32f)
            && ranged.MuzzleFlashSprite.texture.filterMode == FilterMode.Point
            && ranged.MuzzleFlashSprite.texture.mipmapCount == 1,
            "PKA uses Frontier's exact muzzle_bullet sprite (32 px, 32 PPU, point, no mipmaps)");
        Check(flash != null && flashRenderer != null && flashRenderer.sprite == ranged.MuzzleFlashSprite
            && flashRenderer.enabled && flashRenderer.sortingOrder == MuzzleFlashEffect.SortingOrder
            && Vector2.Dot((Vector2)flash.transform.position - (Vector2)hands.transform.position,
                ranged.LastShotDirection) > .5f
            && Vector2.Distance(flash.transform.position, muzzle) < .05f
            && Vector2.Dot(flash.transform.right, ranged.LastShotDirection.normalized) > .999f,
            "firing spawns the muzzle flash at the muzzle, aimed along the fire direction, drawn below mobs");
        Check(ranged.FireSound != null && ranged.FireSound.name == "pka-kinetic-accel"
            && ranged.FireSound.channels == 1 && ranged.FireSound.frequency == 44100
            && Mathf.Abs(ranged.FireSound.length - 1.548866f) < .01f,
            "PKA wires Frontier's exact kinetic_accel gunshot clip (mono 44.1 kHz, 1.55 s)");
        Check(gunshotSource != null && gunshotSource.clip == ranged.FireSound && gunshotSource.isPlaying
            && gunshotSource.spatialBlend == 1f && gunshotSource.rolloffMode == AudioRolloffMode.Logarithmic
            && Approximately(gunshotSource.minDistance, FiringSoundEffect.MinDistance)
            && Approximately(gunshotSource.maxDistance, FiringSoundEffect.MaxDistance)
            && Approximately(gunshotSource.dopplerLevel, 0f)
            && Vector2.Distance(gunshot.transform.position, muzzle) < .05f,
            "firing plays the gunshot as a positional 3D one-shot at the muzzle");
        yield return Capture("pka-firing.png", .07f);
        yield return TapPrimary();
        Check(ranged.ShotsFired == 1, "fire-rate cooldown blocks an immediate duplicate shot");
        yield return new WaitForSecondsRealtime(.35f);
        Check(GameObject.Find(MuzzleFlashEffect.ObjectName) == null,
            "muzzle flash despawns after its 0.4 s TimedDespawn-equivalent lifetime");
        Check(pcmProbe != null && pcmProbe.callbackCount > 0 && pcmProbe.sampleCount > 0
            && pcmProbe.peak > .01f,
            "gunshot reaches the audio mixer as real PCM (callbacks=" + (pcmProbe != null ? pcmProbe.callbackCount : 0)
            + ", samples=" + (pcmProbe != null ? pcmProbe.sampleCount : 0)
            + ", peak=" + (pcmProbe != null ? pcmProbe.peak.ToString("F4") : "n/a")
            + ", rms=" + (pcmProbe != null ? pcmProbe.rms.ToString("F4") : "n/a")
            + ", deviceRate=" + (pcmProbe != null ? pcmProbe.sampleRate : 0) + ")");
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
            && GameObject.Find("Held ranged weapon") == null && !hands.IsTwoHandWielded(pka)
            && !hands.IsHandReserved(HandSystem.HandSlot.Right),
            "switching hands unwields the PKA, clears reservation and hides its visual");
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
        yield return TapSecondary();
        Check(!hands.IsTwoHandWielded(pka) && hands.HeldItem(HandSystem.HandSlot.Right) == bat,
            "PKA cannot wield while the offhand contains another item");

        yield return PressKey(Key.X);
        yield return PressKey(Key.Digit1);
        Check(hands.ReleaseActiveItem(false, mouse.position.ReadValue()) && bat.gameObject.activeSelf,
            "releasing the offhand item makes it available for wielding");
        yield return PressKey(Key.X);
        yield return PressKey(Key.Digit1);
        Check(hands.TryMoveActiveItemTo(HandSystem.HandSlot.Right)
            && hands.HeldItem(HandSystem.HandSlot.Right) == pka
            && hands.HeldItem(HandSystem.HandSlot.Left) == null,
            "PKA transfers to the right hand without duplicate ownership");
        for (int i = 0; i < facings.Length; i++)
        {
            yield return PointAt(player + facingOffsets[i] * 4f);
            Check(HeldVisualMatches(ranged, false, HandSystem.HandSlot.Right, facings[i]),
                "one-handed right PKA uses fixed " + facings[i] + " in-hand art");
        }
        yield return TapSecondary();
        Check(hands.IsTwoHandWielded(pka) && hands.IsHandReserved(HandSystem.HandSlot.Left),
            "right-hand PKA wield reserves the opposite offhand");
        Check(hands.ReleaseActiveItem(false, mouse.position.ReadValue())
            && !hands.IsTwoHandWielded(pka) && !hands.IsHandReserved(HandSystem.HandSlot.Left)
            && hands.HeldItem(HandSystem.HandSlot.Right) == null && pka.gameObject.activeSelf,
            "releasing a wielded PKA clears reservation and item ownership");

        Debug.Log(ok ? "PKA_COMBAT_PASS" : "PKA_COMBAT_FAIL");
        Application.Quit(ok ? 0 : 1);
    }

    bool HasAllHeldSprites(RangedWeapon ranged, bool wielded, HandSystem.HandSlot hand)
    {
        foreach (RangedWeapon.HeldDirection direction in Enum.GetValues(typeof(RangedWeapon.HeldDirection)))
            if (ranged.HeldSprite(wielded, hand, direction) == null) return false;
        return true;
    }

    bool HeldVisualMatches(RangedWeapon ranged, bool wielded, HandSystem.HandSlot owner,
        RangedWeapon.HeldDirection direction)
    {
        var visual = GameObject.Find("Held ranged weapon");
        if (visual == null || visual.transform.localPosition.sqrMagnitude > .0001f
            || Quaternion.Angle(visual.transform.localRotation, Quaternion.identity) > .01f) return false;
        var primary = visual.GetComponent<SpriteRenderer>();
        int order = direction == RangedWeapon.HeldDirection.South ? 20 : 7;
        return primary != null && primary.sortingOrder == order
            && primary.sprite == ranged.HeldSprite(wielded, owner, direction)
            && visual.transform.Find("Offhand layer") == null;
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
