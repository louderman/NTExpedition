using System;
using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

// Kanban card t_fdff8586 observer / regression (opt-in: --crate-drag-playtest), extended for t_c6fac0bc.
//
// Regression for "the authored StorageCrate is draggable, but world-only". Like CrateLoopPlaytest this
// component performs NONE of the acceptance actions itself: it builds the fixture (player beside the real
// crate, world bottle in reach, witness item inside the crate grid), publishes the next intended input to
// <NTEXPEDITION_EVIDENCE_DIR>/plan.json, waits for genuine OS-level input (xdotool on the isolated display)
// and then records PASS/FAIL per clause.
//
// Clauses
//   1  plain click on the crate still opens and closes the storage UI        (normal interaction preserved)
//   2  Ctrl+click on the crate starts a FIXTURE drag of the crate itself, not the nearby loose bottle
//   3  the crate never becomes an ItemComponent drag and never reaches a hand
//   4  Ctrl+click on the loose bottle still drags the bottle                (existing drag path preserved)
//   5  walking keeps the dragged crate on the existing leash
//   6  holding the right button reels the crate toward the cursor, still inside the leash
//   7  releasing Ctrl drops it: same crate, still in the world, grid contents intact, hands still empty
//   8  (t_c6fac0bc) the hand that began the drag is booked as occupied, shown side-specifically in the HUD,
//      and refuses an otherwise-legal pickup; the other hand stays free
//   8b (t_dfdfde35) that HUD feedback includes a recognisable CRATE icon - the crate's own authored body
//      sprite, drawn in its own image beside the label, never in the equipped-item icon slot
//   9  (t_c6fac0bc) Ctrl+RMB steers toward the cursor OFF the crate->player line (sideways, not just inward)
//  10  (t_c6fac0bc) swapping hands mid-hold stops the steer without moving the booking, and the still-held
//      button must not resume it after the booked hand is reselected - only a fresh press steers again
//  11  (t_c6fac0bc) combat mode gates the steer, and releasing Ctrl clears the booking and the HUD state
public class CrateDragPlaytest : MonoBehaviour
{
    const float Cell = 32f;        // InventorySandbox.Cell
    const float PanelPadX = 32f;   // InventorySandbox grid origin: panel.x + 32
    const float PanelPadY = 24f;   // InventorySandbox grid origin: panel.y + 24
    const float DefaultPanelX = 24f;
    const float DefaultPanelY = 70f;

    HandSystem hands;
    PlayerMovement movement;
    HandUI handUi;
    ItemComponent bottle;
    StorageCrate crate;
    ItemDragSystem drag;
    InventorySandbox cratePanel;
    InventorySandbox otherPanel;
    InventoryContainer crateBag;
    Collider2D crateCollider;
    Collider2D bottleCollider;
    ItemComponent witness;
    Vector2Int witnessCell;
    Camera cam;
    string evid = ".";
    int phase;
    bool failed;
    int guiEventsLogged;

    public void Configure(HandSystem h, ItemComponent b) { hands = h; bottle = b; }

    void Awake()
    {
        evid = Environment.GetEnvironmentVariable("NTEXPEDITION_EVIDENCE_DIR");
        if (string.IsNullOrEmpty(evid)) evid = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(evid);
    }

    // ------------------------------------------------------------------ evidence plumbing
    void Log(string message) { Debug.Log("[CRATE_DRAG] " + message); }

    void Fail(string message) { failed = true; Debug.Log("FAIL: " + message); }

    void Pass(string message) { Debug.Log("PASS: " + message); }

    void Expect(string label, bool condition)
    {
        if (condition) Pass(label); else Fail(label);
    }

    void Shot(string name)
    {
        try { ScreenCapture.CaptureScreenshot(Path.Combine(evid, name + ".png")); }
        catch (Exception e) { Log("screenshot " + name + " failed: " + e.Message); }
    }

    // Unity screen point (bottom-left origin) -> xdotool coordinates (top-left origin).
    Vector2 OsPoint(Vector3 world)
    {
        var screen = cam.WorldToScreenPoint(world);
        return new Vector2(screen.x, Screen.height - screen.y);
    }

    // and back: an xdotool point -> the world position under that pixel.
    Vector3 WorldAtOs(Vector2 os)
    {
        return cam.ScreenToWorldPoint(new Vector3(os.x, Screen.height - os.y, -cam.transform.position.z));
    }

    Rect PanelRect(InventorySandbox panel)
    {
        var dimensions = panel.bag.ContainerDimensions;
        float width = dimensions.x * Cell + PanelPadX;
        float height = dimensions.y * Cell + PanelPadY;
        if (CenterOnScreen(panel))
            return new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        return new Rect(DefaultPanelX, DefaultPanelY, width, height);
    }

    static bool CenterOnScreen(InventorySandbox panel)
    {
        var field = typeof(InventorySandbox).GetField("centerOnScreen",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field != null && panel != null && (bool)field.GetValue(panel);
    }

    void PublishPlan(string kind, Vector2 point, string key, string label, string shot)
    {
        phase++;
        var sb = new System.Text.StringBuilder();
        sb.Append("{\n");
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"phase\": {0},\n", phase);
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"kind\": \"{0}\",\n", kind);
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"x\": {0:0},\n", point.x);
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"y\": {0:0},\n", point.y);
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"key\": \"{0}\",\n", key ?? "");
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"seconds\": {0:0.0},\n", walkSeconds);
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"shot\": \"{0}\",\n", shot ?? "");
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"label\": \"{0}\"\n", label);
        sb.Append("}\n");
        var path = Path.Combine(evid, "plan.json");
        File.WriteAllText(path + ".tmp", sb.ToString());
        if (File.Exists(path)) File.Delete(path);
        File.Move(path + ".tmp", path);
        Log("plan phase " + phase + ": " + kind + " '" + label + "' at xdotool (" +
            point.x.ToString("0", CultureInfo.InvariantCulture) + "," +
            point.y.ToString("0", CultureInfo.InvariantCulture) + ")");
    }

    void PublishDone(bool ok)
    {
        var path = Path.Combine(evid, "plan.json");
        File.WriteAllText(path + ".tmp",
            "{\n  \"phase\": 99,\n  \"done\": true,\n  \"ok\": " + (ok ? "true" : "false") + "\n}\n");
        if (File.Exists(path)) File.Delete(path);
        File.Move(path + ".tmp", path);
    }

    // The drag leash only binds once the player is beyond MaxDragRadius, so a walk short enough to stay inside
    // the leash proves nothing about following: 1.5s could not get out of it in a ship corridor.
    float walkSeconds = 6f;

    IEnumerator Expect(string label, Func<bool> condition, float timeout)
    {
        float start = Time.realtimeSinceStartup;
        while (!condition())
        {
            if (Time.realtimeSinceStartup - start > timeout)
            {
                Fail("timeout after " + timeout + "s waiting for: " + label);
                yield break;
            }
            yield return null;
        }
        Log("observed: " + label);
    }

    IEnumerator Settle(int frames)
    {
        for (int i = 0; i < frames; i++) yield return null;
    }

    // ------------------------------------------------------------------ state inspection
    bool BagContains(InventoryContainer container, ItemComponent item)
    {
        if (container == null || item == null) return false;
        var dimensions = container.ContainerDimensions;
        for (int y = 0; y < dimensions.y; y++)
            for (int x = 0; x < dimensions.x; x++)
                if (container.GetItem(x, y) == item) return true;
        return false;
    }

    int CountBagCells(InventoryContainer container, ItemComponent item)
    {
        int count = 0;
        if (container == null || item == null) return 0;
        var dimensions = container.ContainerDimensions;
        for (int y = 0; y < dimensions.y; y++)
            for (int x = 0; x < dimensions.x; x++)
                if (container.GetItem(x, y) == item) count++;
        return count;
    }

    int OccupiedBagCells(InventoryContainer container)
    {
        int count = 0;
        if (container == null) return 0;
        var dimensions = container.ContainerDimensions;
        for (int y = 0; y < dimensions.y; y++)
            for (int x = 0; x < dimensions.x; x++)
                if (container.GetItem(x, y) != null) count++;
        return count;
    }

    bool HandsEmpty()
    {
        return hands != null && hands.HeldItem(HandSystem.HandSlot.Left) == null &&
               hands.HeldItem(HandSystem.HandSlot.Right) == null;
    }

    // The player starts with the loadout weapon in one hand, so "did the crate get picked up" is measured
    // against the ACTIVE hand plus the identity of what the hands hold, never against both hands being free.
    bool ActiveHandEmpty() => hands != null && hands.HeldItem(hands.CurrentHandSlot) == null;

    // plan.json holds ONE step at a time and the driver polls for its exact phase number, so publishing two
    // steps in the same frame makes the driver miss the first one and wait forever. Every publish is therefore
    // separated by a wait for the previous step's effect - here, that the drag modifier is genuinely held.
    static bool CtrlHeldDown()
    {
        return Keyboard.current != null &&
               (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
    }

    // Which of the walk keys the driver is currently holding, so the walk step can be proven delivered.
    static bool WalkKeyHeld(string key)
    {
        if (Keyboard.current == null) return false;
        switch (key)
        {
            case "a": return Keyboard.current.aKey.isPressed;
            case "d": return Keyboard.current.dKey.isPressed;
            case "w": return Keyboard.current.wKey.isPressed;
            case "s": return Keyboard.current.sKey.isPressed;
            default: return false;
        }
    }

    Vector2 PlayerBox(float shrink)
    {
        var body = hands.GetComponent<Collider2D>();
        if (body == null) body = hands.GetComponentInChildren<Collider2D>();
        return body != null ? (Vector2)body.bounds.size * shrink : new Vector2(0.4f, 0.4f);
    }

    // True when a player-sized box centred on <spot> touches nothing solid that is not the player or the crate.
    bool BodyFree(Vector2 spot)
    {
        foreach (var hit in Physics2D.OverlapBoxAll(spot, PlayerBox(0.9f), 0f))
        {
            if (hit == null || hit.isTrigger) continue;
            if (hit.transform.root == hands.transform.root) continue;
            if (crateCollider != null && hit == crateCollider) continue;
            return false;
        }
        return true;
    }

    // How far a player-sized box can travel from <origin> along <direction> before it meets something solid.
    float ClearCorridor(Vector2 origin, Vector2 direction)
    {
        float clear = 0f;
        for (float travelled = 0.25f; travelled <= 6f; travelled += 0.25f)
        {
            foreach (var hit in Physics2D.OverlapBoxAll(origin + direction * travelled, PlayerBox(0.7f), 0f))
            {
                if (hit == null || hit.isTrigger) continue;
                if (hit.transform.root == hands.transform.root) continue;
                return clear;
            }
            clear = travelled;
        }
        return clear;
    }

    // Places the player on the clearest standing spot beside the crate and returns the key that walks out
    // along that spot's clearest corridor. Every candidate is reported, so the run's log says which geometry
    // forced the choice instead of leaving a silent "the player did not move".
    string PlacePlayerBesideCrate(Vector3 crateStart)
    {
        var probes = new[]
        {
            new Vector3(-1.35f, 0f, 0f), new Vector3(1.35f, 0f, 0f),
            new Vector3(0f, -1.35f, 0f), new Vector3(0f, 1.35f, 0f),
            new Vector3(-1.8f, 0f, 0f), new Vector3(1.8f, 0f, 0f),
            new Vector3(0f, -1.8f, 0f), new Vector3(0f, 1.8f, 0f),
        };
        var walkKeys = new[] { "a", "d", "s", "w" };
        var walkDirs = new[] { Vector2.left, Vector2.right, Vector2.down, Vector2.up };
        string bestKey = "a";
        Vector2 bestSpot = (Vector2)crateStart + new Vector2(-1.35f, 0f);
        float bestClear = -1f;
        foreach (var probe in probes)
        {
            Vector2 spot = (Vector2)crateStart + (Vector2)probe;
            if (!BodyFree(spot))
            {
                Log("stand probe " + spot.ToString("0.00") + ": blocked");
                continue;
            }
            for (int i = 0; i < walkKeys.Length; i++)
            {
                float corridor = ClearCorridor(spot, walkDirs[i]);
                Log("stand probe " + spot.ToString("0.00") + " walk " + walkKeys[i] + ": corridor=" +
                    corridor.ToString("F2") + "m");
                if (corridor > bestClear)
                {
                    bestClear = corridor;
                    bestKey = walkKeys[i];
                    bestSpot = spot;
                }
            }
        }
        hands.transform.position = new Vector3(bestSpot.x, bestSpot.y, crateStart.z);
        Log("player placed at " + bestSpot.ToString("0.00") + " walking key=" + bestKey +
            " corridor=" + bestClear.ToString("F2") + "m (leash=" + drag.MaxDragRadius.ToString("F2") + "m)");
        return bestKey;
    }

    bool InEitherHand(ItemComponent item)
    {
        return hands != null && item != null &&
               (hands.HeldItem(HandSystem.HandSlot.Left) == item ||
                hands.HeldItem(HandSystem.HandSlot.Right) == item);
    }

    string HandState()
    {
        return "activeHand=" + (hands.HeldItem(hands.CurrentHandSlot) == null ? "empty" : hands.HeldItem(hands.CurrentHandSlot).name) +
               " otherHand=" + (hands.HeldItem(hands.OtherHand(hands.CurrentHandSlot)) == null ? "empty" : hands.HeldItem(hands.OtherHand(hands.CurrentHandSlot)).name);
    }

    string CrateState()
    {
        return "cratePos=" + crate.transform.position.ToString("F2") +
               " playerPos=" + hands.transform.position.ToString("F2") +
               " distance=" + Vector2.Distance(crate.transform.position, hands.transform.position).ToString("F2") +
               " leashed=" + (Vector2.Distance(crate.transform.position, hands.transform.position) <= drag.MaxDragRadius + .25f) +
               " dragging=" + drag.IsDragging +
               " draggedItem=" + (drag.DraggedItem == null ? "none" : drag.DraggedItem.name) +
               " draggedFixture=" + (drag.DraggedTarget == null ? "none" : drag.DraggedTarget.DragLabel) +
               " cratePanelEnabled=" + (cratePanel != null && cratePanel.enabled) +
               " crateIsItemComponent=" + (crate.GetComponent<ItemComponent>() != null) +
               " crateBagOccupiedCells=" + OccupiedBagCells(crateBag) +
               " crateBagHoldsWitness=" + BagContains(crateBag, witness);
    }

    // Diagnostic only: records the raw IMGUI events this observer sees, so the run log proves the real OS
    // pointer/keyboard events reached the game's IMGUI layer at all.
    void OnGUI()
    {
        var e = Event.current;
        if (e == null || guiEventsLogged >= 80) return;
        if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.KeyDown ||
            e.type == EventType.MouseMove || e.type == EventType.ScrollWheel)
        {
            guiEventsLogged++;
            if (guiEventsLogged <= 24)
                Log("gui event #" + guiEventsLogged + " " + e.type + " button=" + e.button + " key=" + e.keyCode +
                    " guiMouse=" + e.mousePosition.ToString("0"));
        }
    }

    // ------------------------------------------------------------------ steering-gate helpers (t_c6fac0bc)
    string GateState() =>
        "steering=" + drag.IsSteering + " steerHand=" + (drag.SteeringHand.HasValue ? drag.SteeringHand.Value.ToString() : "none") +
        " booked=" + BookedHand() + " hudOccupied=" + HudOccupied() +
        " combat=" + (movement != null && movement.combatMode) +
        " currentHand=" + (hands == null ? "?" : hands.CurrentHandSlot.ToString());
    string BookedHand() => hands == null || !hands.HasDragReservation ? "none" : hands.DragReservedHand.ToString();
    string HudOccupied()
    {
        if (handUi == null) return "no-ui";
        if (handUi.IsOccupiedShown(HandSystem.HandSlot.Left)) return "Left";
        return handUi.IsOccupiedShown(HandSystem.HandSlot.Right) ? "Right" : "none";
    }

    // Picks the direction most perpendicular to the crate->player axis - so that "moved toward the cursor"
    // cannot be satisfied by simply reeling the crate inward - among the directions the crate can actually
    // travel through. Clearance is probed first because a blocked direction would measure the collision sweep
    // instead of the steer.
    Vector2 LateralDir(Vector2 cratePos, Vector2 playerPos, out float clearance)
    {
        Vector2 axis = playerPos - cratePos;
        axis = axis.sqrMagnitude < .0001f ? Vector2.right : axis.normalized;
        Vector2 best = Vector2.zero;
        float bestScore = float.NegativeInfinity;
        clearance = 0f;
        for (int pass = 0; pass < 2 && best == Vector2.zero; pass++)
        {
            float minClear = pass == 0 ? 1f : -1f;
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float clear = CrateClearance(cratePos, dir);
                if (clear < minClear) continue;
                float lateral = 1f - Mathf.Abs(Vector2.Dot(dir, axis));   // 1 = fully sideways, 0 = straight at the player
                float score = lateral * 10f + Mathf.Min(clear, 2f);
                if (score > bestScore) { bestScore = score; best = dir; clearance = clear; }
            }
        }
        return best == Vector2.zero ? Vector2.right : best;
    }

    float CrateClearance(Vector2 origin, Vector2 dir)
    {
        Vector2 size = crateCollider == null ? Vector2.one : (Vector2)crateCollider.bounds.size * .8f;
        for (float t = .4f; t <= 2.01f; t += .2f)
        {
            var hits = Physics2D.OverlapBoxAll(origin + dir * t, size, 0f);
            foreach (var h in hits)
            {
                if (h == null || h.isTrigger || h == crateCollider || h == bottleCollider) continue;
                if (hands != null && (h.transform == hands.transform || h.transform.IsChildOf(hands.transform))) continue;
                return t;
            }
        }
        return 2f;
    }

    // ------------------------------------------------------------------ main flow
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(1.5f);

        cam = Camera.main;
        crate = UnityEngine.Object.FindFirstObjectByType<StorageCrate>(FindObjectsInactive.Include);
        var panels = UnityEngine.Object.FindObjectsByType<InventorySandbox>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var panel in panels)
        {
            if (panel == null || panel.bag == null) continue;
            if (crate != null && panel.GetComponent<StorageCrate>() == crate) cratePanel = panel;
            else if (otherPanel == null) otherPanel = panel;
        }
        if (crate != null) crateCollider = crate.GetComponent<Collider2D>();
        if (bottle != null) bottleCollider = bottle.GetComponent<Collider2D>();
        crateBag = cratePanel == null ? null : cratePanel.bag;
        drag = hands == null ? null : hands.GetComponent<ItemDragSystem>();
        if (drag == null) drag = UnityEngine.Object.FindFirstObjectByType<ItemDragSystem>();
        movement = hands == null ? null : hands.GetComponent<PlayerMovement>();
        handUi = UnityEngine.Object.FindFirstObjectByType<HandUI>(FindObjectsInactive.Include);

        Log("setup: screen=" + Screen.width + "x" + Screen.height +
            " camera=" + (cam == null ? "MISSING" : (cam.orthographic ? "ortho " + cam.orthographicSize : "persp")) +
            " crate=" + (crate == null ? "MISSING" : crate.name + " isIDragTarget=" + (crate is IDragTarget)) +
            " cratePanel=" + (cratePanel == null ? "MISSING" : cratePanel.name) +
            " crateBag=" + (crateBag == null ? "-" : crateBag.ContainerDimensions.ToString()) +
            " dragSystem=" + (drag == null ? "MISSING" : drag.name + " leash=" + drag.MaxDragRadius) +
            " bottle=" + (bottle == null ? "MISSING" : bottle.name));

        if (cam == null || crate == null || cratePanel == null || crateBag == null || crateCollider == null ||
            hands == null || bottle == null || drag == null || movement == null)
        {
            Fail("scene is missing one of: camera/crate/crate panel/crate container/player hands/world bottle/drag system/player movement");
            Finish();
            yield break;
        }
        if (handUi == null) Log("WARNING: no HandUI in the scene - the occupied-state clauses will be reported as no-ui");

        // ---- fixture (setup only, no acceptance action): player beside the crate, bottle in reach. The
        // standing spot is probed rather than assumed - the authored position sits right against ship
        // geometry (walking left moved the player 0 m), and the walk clause needs a real corridor for the
        // leash to pull the crate down.
        var crateStart = crate.transform.position;
        string walkKey = PlacePlayerBesideCrate(crateStart);
        bottle.transform.position = hands.transform.position + new Vector3(0.75f, -0.55f, 0f);
        Physics2D.SyncTransforms();
        yield return Settle(3);   // camera follows in LateUpdate

        // Witness for "contents intact": one spare item placed in the crate's grid through the game's own
        // container API, checked again after the whole drag cycle. Best effort - if the container refuses it
        // the run says so and falls back to comparing the grid's occupied-cell count.
        var witnessPrefab = Resources.Load<GameObject>("WorldItems/WaterBottle");
        if (witnessPrefab != null)
        {
            var witnessObject = UnityEngine.Object.Instantiate(witnessPrefab, crateStart, Quaternion.identity);
            witness = witnessObject.GetComponent<ItemComponent>();
            if (witness != null && crateBag.TryAutoPlace(witness)) witnessCell = witness.GridPosition;
            else witness = null;
        }
        Log("witness item in the crate grid at setup: " +
            (witness == null ? "REFUSED (occupied-cell count only)" : "cell " + witnessCell));
        // The authored crate contents are not necessarily one cell, so "contents untouched" is measured
        // against the setup count instead of an assumed number.
        int crateBagCellsAtSetup = OccupiedBagCells(crateBag);
        Physics2D.SyncTransforms();
        yield return Settle(2);

        var cratePoint = OsPoint(crateCollider.bounds.center);
        var bottlePoint = OsPoint(bottle.transform.position);
        bool bottleUnderCratePoint = bottleCollider != null && bottleCollider.OverlapPoint(WorldAtOs(cratePoint));
        Log("fixture: bottle xdotool=" + bottlePoint.ToString("0") + " crate xdotool=" + cratePoint.ToString("0") +
            " crateOverlapPoint=" + crateCollider.OverlapPoint(WorldAtOs(cratePoint)) +
            " bottleAlsoUnderCratePoint=" + bottleUnderCratePoint +
            " playerCrateDistance=" +
            Vector2.Distance(hands.transform.position, crateCollider.ClosestPoint(hands.transform.position)).ToString("0.00"));

        Expect("player is inside crate interaction and drag range",
            Vector2.Distance(hands.transform.position, crateCollider.ClosestPoint(hands.transform.position)) <= 2f);
        Expect("the crate click point is clear of every storage panel",
            !InventorySandbox.PointerOverStorage(new Vector2(cratePoint.x, Screen.height - cratePoint.y)));
        Expect("the drag system has the crate registered as a world fixture",
            ItemDragSystem.IsFixtureRegistered(crate));
        // The baseline (t_3dae019b) failed exactly here: the click landed on the crate but only the loose
        // bottle was ever a drag candidate. Recording the overlap makes this run's fixture directly comparable.
        if (bottleUnderCratePoint)
            Log("WARNING: the loose bottle also covers the crate click point - the ranking still has to pick the crate");
        if (failed) { Finish(); yield break; }

        // ---- the player starts the scene holding the loadout weapon, so free the active hand first.
        if (!ActiveHandEmpty())
        {
            PublishPlan("key", Vector2.zero, "x", "press X to empty the active hand", null);
            yield return Expect("active hand is empty after the swap-hands key",
                () => hands.HeldItem(hands.CurrentHandSlot) == null, 15f);
            if (failed) { Finish(); yield break; }
        }
        Log("after hand setup: " + HandState());

        Shot("01-fixture");
        Log(CrateState());

        // ============================================================ 1. normal interaction still works
        PublishPlan("click", cratePoint, null, "plain left click the crate (must open the storage UI)", "02-crate-open");
        yield return Expect("plain click on the crate opens the storage panel", () => cratePanel.enabled, 20f);
        if (failed) { Finish(); yield break; }
        PublishPlan("click", cratePoint, null, "plain left click again (must close the storage UI)", "03-crate-closed");
        yield return Expect("plain click on the crate closes the storage panel again", () => !cratePanel.enabled, 20f);
        if (failed) { Finish(); yield break; }
        Log("after normal interaction: " + CrateState());

        // ============================================================ 2. Ctrl+click drags the crate itself
        PublishPlan("ctrl-down", Vector2.zero, null, "hold Ctrl (the drag modifier)", null);
        yield return Expect("Ctrl is held before the drag click", () => CtrlHeldDown(), 15f);
        PublishPlan("click", cratePoint, null, "Ctrl + left click the crate", "04-ctrl-click-crate");
        yield return Expect("Ctrl+click took hold of the crate as a world fixture",
            () => drag.IsDragging && drag.DraggedTarget != null && drag.DraggedTarget.DragLabel == crate.name, 20f);
        if (failed) { Shot("04-ctrl-click-crate-failed"); Finish(); yield break; }

        Expect("the fixture drag did not touch the nearby loose bottle",
            bottle.gameObject.activeInHierarchy && drag.DraggedItem == null &&
            !BagContains(crateBag, bottle) && !BagContains(otherPanel == null ? null : otherPanel.bag, bottle));
        Expect("the crate did not become an ItemComponent drag", drag.DraggedItem == null);
        Expect("the Ctrl+click did not open the crate", !cratePanel.enabled);
        Expect("nothing was acquired into a hand", ActiveHandEmpty() && !InEitherHand(bottle) && !InEitherHand(witness));
        Expect("the crate is still not an ItemComponent", crate.GetComponent<ItemComponent>() == null);
        Log("after crate grab: " + CrateState());

        // ============================================================ 2b. Ctrl+RMB steering + the hand booking
        // New for t_c6fac0bc, driven entirely by real OS input from the driver. The two clauses marked
        // "API guard" are the only ones that call the game's own acquisition entry point directly, and they
        // are labelled as such in the report.
        var dragHand = hands.CurrentHandSlot;
        var otherHand = hands.OtherHand(dragHand);
        Log("gate section begins: " + GateState());
        Expect("the drag booked the hand that began it as the steering hand", drag.SteeringHand == dragHand);
        Expect("that hand is reserved as occupied for the whole drag", hands.IsDragHandReserved(dragHand));
        Expect("the other hand is NOT reserved", !hands.IsDragHandReserved(otherHand));
        Expect("the HUD shows the occupied state on the dragging hand only",
            handUi != null && handUi.IsOccupiedShown(dragHand) && !handUi.IsOccupiedShown(otherHand));
        var hudRect = handUi == null ? new Rect() : handUi.OccupiedLabelScreenRect(dragHand);
        Log("HUD occupied label \"" + (handUi == null ? "?" : handUi.OccupiedText) + "\" screen rect " +
            hudRect.ToString("0") + " on a " + Screen.width + "x" + Screen.height + " screen");
        Expect("the occupied label is on screen",
            hudRect.width > 1f && hudRect.xMin >= 0f && hudRect.xMax <= Screen.width &&
            hudRect.yMin >= 0f && hudRect.yMax <= Screen.height);
        // t_dfdfde35: the occupied hand has to show a recognisable CRATE icon, not only the text label. The
        // sprite must be the crate's OWN authored art (the SS14 `base` layer the world renderer draws), so the
        // icon can never drift from what is on screen in the world, and the indicator must be its own image -
        // the held-item icon in the slot stays free so an equipped item still renders normally. The pre-fix
        // (text-only) build is the RED control for these clauses.
        var crateRenderer = crate == null ? null : crate.GetComponent<SpriteRenderer>();
        var crateSprite = crateRenderer == null ? null : crateRenderer.sprite;
        var iconSprite = handUi == null ? null : handUi.OccupiedIconSprite(dragHand);
        var iconRect = handUi == null ? new Rect() : handUi.OccupiedIconScreenRect(dragHand);
        Log("HUD occupied icon sprite " + (iconSprite == null ? "none" : iconSprite.name) +
            " (crate body sprite " + (crateSprite == null ? "none" : crateSprite.name) + ") screen rect " +
            iconRect.ToString("0"));
        Expect("the HUD shows an occupied icon on the dragging hand only",
            handUi != null && handUi.IsOccupiedIconShown(dragHand) && !handUi.IsOccupiedIconShown(otherHand));
        Expect("the occupied icon is the crate's own authored sprite, not a stand-in",
            iconSprite != null && crateSprite != null && iconSprite == crateSprite);
        Expect("the occupied icon is its own image, not the held-item icon",
            handUi != null && handUi.LeftOccupiedIcon != null && handUi.RightOccupiedIcon != null &&
            handUi.LeftOccupiedIcon != handUi.LeftItemIcon && handUi.LeftOccupiedIcon != handUi.RightItemIcon &&
            handUi.RightOccupiedIcon != handUi.LeftItemIcon && handUi.RightOccupiedIcon != handUi.RightItemIcon);
        Expect("the occupied icon is on screen beside the label without overlapping it",
            iconRect.width > 1f && iconRect.xMin >= 0f && iconRect.xMax <= Screen.width &&
            iconRect.yMin >= 0f && iconRect.yMax <= Screen.height && !iconRect.Overlaps(hudRect));
        // API guard probes (not OS input - labelled in the report): the bottle is in reach and the booked hand
        // is empty, so the booking is the only reason its own pickup entry point can refuse.
        Expect("API guard: the reserved hand refuses an otherwise-legal pickup",
            !hands.TryPickup(bottle) && hands.HeldItem(dragHand) == null && bottle.Container == null);
        Expect("API guard: the refusal is side-specific - the other hand is not booked",
            !hands.IsDragHandReserved(otherHand) && drag.SteeringHand == dragHand);
        Shot("g1-hand-booked");

        // ---- combat mode: the steer must not tick, and entering combat is also the pre-existing path that
        // ends a fixture drag outright, so the clause checks both halves.
        PublishPlan("key", Vector2.zero, "1", "toggle combat mode on (Ctrl is still held)", null);
        yield return Expect("entering combat ended the fixture drag (pre-existing behaviour)", () => !drag.IsDragging, 15f);
        if (failed) { Finish(); yield break; }
        yield return Settle(4);
        Expect("the steer is off after the combat gate", !drag.IsSteering);
        Expect("the hand booking was released with the drag", !hands.HasDragReservation);
        Expect("the HUD cleared the occupied state",
            handUi == null || (!handUi.IsOccupiedShown(HandSystem.HandSlot.Left) &&
                               !handUi.IsOccupiedShown(HandSystem.HandSlot.Right) &&
                               !handUi.IsOccupiedIconShown(HandSystem.HandSlot.Left) &&
                               !handUi.IsOccupiedIconShown(HandSystem.HandSlot.Right)));
        var inCombat = crate.transform.position;
        yield return new WaitForSecondsRealtime(.8f);
        Expect("the crate did not keep moving after the drag ended in combat",
            Vector2.Distance(crate.transform.position, inCombat) < .05f);
        PublishPlan("key", Vector2.zero, "1", "toggle combat mode off again", "g2-combat-drop");
        yield return Expect("combat mode is off again", () => !movement.combatMode, 15f);
        if (failed) { Finish(); yield break; }

        // ---- take hold again, then steer sideways with the right button
        PublishPlan("click", cratePoint, null, "Ctrl + left click the crate again (fresh booking)", "g3-regrabbed");
        yield return Expect("the crate can be taken hold of again with a fresh booking",
            () => drag.IsDragging && drag.DraggedTarget != null && drag.SteeringHand == hands.CurrentHandSlot, 20f);
        if (failed) { Finish(); yield break; }
        Expect("the re-grab booked the selected hand and the HUD shows it again",
            hands.IsDragHandReserved(hands.CurrentHandSlot) &&
            (handUi == null || (handUi.IsOccupiedShown(hands.CurrentHandSlot) &&
                                handUi.IsOccupiedIconShown(hands.CurrentHandSlot))));

        // The steer target sits OFF the crate-to-player line, so "moved toward the cursor" cannot be satisfied
        // by reeling the crate inward. The direction is probed for clearance first.
        var steerBase = (Vector2)crate.transform.position;
        var playerBase = (Vector2)hands.transform.position;
        float steerClearance;
        var steerDir = LateralDir(steerBase, playerBase, out steerClearance);
        var steerWorld = steerBase + steerDir * .7f;
        var steerPoint = OsPoint(steerWorld);
        var toCursor = (steerWorld - steerBase).normalized;
        var toPlayer = (playerBase - steerBase).normalized;
        Log("lateral steer: target " + steerWorld.ToString("F2") + " dir " + steerDir.ToString("F2") +
            " clearance " + steerClearance.ToString("F2") + "m crateToPlayer " + toPlayer.ToString("F2") +
            " dot(dir,toPlayer)=" + Vector2.Dot(steerDir, toPlayer).ToString("F2"));
        PublishPlan("rmb-down", steerPoint, null,
            "hold the right button off the crate-to-player line: the crate must steer sideways", null);
        yield return Expect("holding the right button steers the crate", () => drag.IsSteering, 12f);
        yield return Expect("the crate moved once the steer was armed",
            () => Vector2.Distance(crate.transform.position, steerBase) > .3f, 15f);
        if (failed) { Shot("g4-lateral-steer-failed"); Finish(); yield break; }
        var steerMove = (Vector2)crate.transform.position - steerBase;
        Log("lateral steer result: moved " + steerMove.ToString("F2") + " |moved| " + steerMove.magnitude.ToString("F2") +
            " dot(moved,toCursor)=" + Vector2.Dot(steerMove.normalized, toCursor).ToString("F2") +
            " dot(moved,toPlayer)=" + Vector2.Dot(steerMove.normalized, toPlayer).ToString("F2"));
        Expect("the crate steered toward the cursor",
            steerMove.magnitude > .3f && Vector2.Dot(steerMove.normalized, toCursor) > .5f);
        Expect("the steer was lateral, not merely inward toward the player",
            Vector2.Dot(steerMove.normalized, toCursor) > Vector2.Dot(steerMove.normalized, toPlayer) + .25f);
        Expect("the steer kept the crate inside the drag leash",
            Vector2.Distance(crate.transform.position, hands.transform.position) <= drag.MaxDragRadius + .25f);
        Shot("g4-lateral-steer");

        // ---- swap hands with the button still held: the steer stops, the booking must not follow
        PublishPlan("key", Vector2.zero, "x", "swap hands while the right button is still held", null);
        yield return Expect("the selected hand is now the other one", () => hands.CurrentHandSlot == otherHand, 15f);
        if (failed) { Finish(); yield break; }
        yield return Settle(4);
        Expect("swapping hands did not move the booking", hands.IsDragHandReserved(dragHand) && !hands.IsDragHandReserved(otherHand));
        Expect("the steer stopped the moment the booked hand stopped being selected", !drag.IsSteering);
        Expect("the drag itself survived the hand swap", drag.IsDragging && drag.DraggedTarget != null);
        Expect("the HUD still shows the occupied state and icon on the original hand",
            handUi != null && handUi.IsOccupiedShown(dragHand) && !handUi.IsOccupiedShown(otherHand) &&
            handUi.IsOccupiedIconShown(dragHand) && !handUi.IsOccupiedIconShown(otherHand));
        var gatedAt = crate.transform.position;
        yield return new WaitForSecondsRealtime(1.2f);
        Log("drift while the booked hand was deselected: " + Vector2.Distance(crate.transform.position, gatedAt).ToString("F3") + "m " + GateState());
        Expect("no stale movement leaked while the booked hand was deselected",
            Vector2.Distance(crate.transform.position, gatedAt) < .05f);
        Shot("g5-gated-hand-swap");

        // ---- reselect the booked hand with the button STILL held: the gate reopens, the stale hold must not
        PublishPlan("key", Vector2.zero, "x", "swap back to the booked hand (the right button is still held)", null);
        yield return Expect("the booked hand is selected again", () => hands.CurrentHandSlot == dragHand, 15f);
        if (failed) { Finish(); yield break; }
        yield return Settle(4);
        var reselectedAt = crate.transform.position;
        yield return new WaitForSecondsRealtime(1.2f);
        Log("drift after reselecting the booked hand: " + Vector2.Distance(crate.transform.position, reselectedAt).ToString("F3") + "m " + GateState());
        Expect("the still-held right button did not restart the steer by itself", !drag.IsSteering);
        Expect("no stale movement leaked after the booked hand was reselected",
            Vector2.Distance(crate.transform.position, reselectedAt) < .05f);

        // ---- a FRESH press with the gate open steers again
        PublishPlan("rmb-up", Vector2.zero, null, "release the right button (the drag must survive)", null);
        yield return Expect("the driver released the right button",
            () => Mouse.current == null || !Mouse.current.rightButton.isPressed, 10f);
        if (failed) { Finish(); yield break; }
        yield return Expect("the drag survived the gated right-button release", () => drag.IsDragging, 5f);
        var freshBase = (Vector2)crate.transform.position;
        var freshWorld = freshBase + steerDir * .4f;
        PublishPlan("rmb-down", OsPoint(freshWorld), null,
            "press the right button again: a fresh press must steer again", null);
        yield return Expect("a fresh right-button press steers again", () => drag.IsSteering, 12f);
        yield return Expect("the crate moved again on the fresh press",
            () => Vector2.Distance(crate.transform.position, freshBase) > .2f, 15f);
        if (failed) { Shot("g6-fresh-press-failed"); Finish(); yield break; }
        yield return Expect("the right button was really held down by the driver",
            () => Mouse.current != null && Mouse.current.rightButton.isPressed, 10f);
        Shot("g6-fresh-press");
        PublishPlan("rmb-up", Vector2.zero, null, "release the right button (keep dragging)", null);
        yield return Expect("the driver released the right button again",
            () => Mouse.current == null || !Mouse.current.rightButton.isPressed, 10f);
        if (failed) { Finish(); yield break; }
        yield return Settle(3);
        Expect("releasing the right button stopped the steer but kept the drag", !drag.IsSteering && drag.IsDragging);

        // ============================================================ 2c. release: booking and HUD clear too
        PublishPlan("ctrl-up", Vector2.zero, null, "release Ctrl (the crate must be dropped where it stands)", "05-after-crate-drop");
        yield return Expect("releasing Ctrl ended the fixture drag", () => !drag.IsDragging, 15f);
        if (failed) { Finish(); yield break; }
        yield return Settle(4);
        Expect("releasing Ctrl released the hand booking", !hands.HasDragReservation);
        Expect("releasing Ctrl cleared the HUD occupied state",
            handUi == null || (!handUi.IsOccupiedShown(HandSystem.HandSlot.Left) &&
                               !handUi.IsOccupiedShown(HandSystem.HandSlot.Right) &&
                               !handUi.IsOccupiedIconShown(HandSystem.HandSlot.Left) &&
                               !handUi.IsOccupiedIconShown(HandSystem.HandSlot.Right)));
        Expect("the gate section acquired nothing into either hand",
            ActiveHandEmpty() && !InEitherHand(bottle) && !InEitherHand(witness) &&
            crate.GetComponent<ItemComponent>() == null);
        Log("after gate section drop: " + GateState());
        Shot("g7-after-gate-drop");
        // The gate section steered the crate sideways, so every later crate click uses its new position.
        cratePoint = OsPoint(crateCollider.bounds.center);
        Expect("the dropped crate is the same object, still in the world, contents untouched",
            crate.gameObject.activeInHierarchy &&
            OccupiedBagCells(crateBag) == crateBagCellsAtSetup &&
            BagContains(crateBag, witness) == (witness != null));
        Expect("the crate was not carried into any hand",
            crate.transform.parent == null || crate.transform.parent.GetComponent<HandSystem>() == null);
        Log("after crate drop: " + CrateState());

        // ============================================================ 3. loose item drag still works
        PublishPlan("ctrl-down", Vector2.zero, null, "hold Ctrl again", null);
        yield return Expect("Ctrl is held before the loose-bottle click", () => CtrlHeldDown(), 15f);
        PublishPlan("click", bottlePoint, null, "Ctrl + left click the loose water bottle", "06-ctrl-click-bottle");
        yield return Expect("Ctrl+click on the loose bottle still drags the bottle",
            () => drag.IsDragging && drag.DraggedItem == bottle, 20f);
        if (failed) { Shot("06-ctrl-click-bottle-failed"); Finish(); yield break; }

        Expect("the bottle drag is an item drag, not a fixture drag", drag.DraggedTarget == null);
        PublishPlan("ctrl-up", Vector2.zero, null, "release Ctrl (drop the bottle)", "07-after-bottle-drop");
        yield return Expect("releasing Ctrl ended the bottle drag", () => !drag.IsDragging, 15f);
        if (failed) { Finish(); yield break; }
        Expect("the dropped bottle stayed a loose world item",
            bottle.gameObject.activeInHierarchy && bottle.Container == null &&
            !BagContains(crateBag, bottle) && CountBagCells(crateBag, bottle) == 0);
        Expect("the loose-item regression acquired nothing into a hand",
            ActiveHandEmpty() && !InEitherHand(bottle) && !InEitherHand(witness));
        Log("after bottle drop: " + CrateState());

        // ============================================================ 4. grab the crate again and walk
        PublishPlan("ctrl-down", Vector2.zero, null, "hold Ctrl a third time", null);
        yield return Expect("Ctrl is held before the second crate click", () => CtrlHeldDown(), 15f);
        PublishPlan("click", cratePoint, null, "Ctrl + left click the crate again", "08-crate-regrabbed");
        yield return Expect("the crate can be taken hold of again after a drop",
            () => drag.IsDragging && drag.DraggedTarget != null && drag.DraggedTarget.DragLabel == crate.name, 20f);
        if (failed) { Finish(); yield break; }

        var beforeWalk = crate.transform.position;
        PublishPlan("walk", Vector2.zero, walkKey, "hold the walk key with Ctrl still held: the crate must follow", null);
        yield return Expect("the crate followed the player and stayed inside the drag leash",
            () => drag.IsDragging &&
                  Vector2.Distance(crate.transform.position, hands.transform.position) <= drag.MaxDragRadius + .25f &&
                  Vector2.Distance(crate.transform.position, beforeWalk) > .3f, 25f);
        if (failed) { Shot("09-walk-follow-failed"); Finish(); yield break; }
        // A step is consumed only when its effect is gone: the pull must not be measured while the player is
        // still mid-walk, so the walk key release is the proof that the driver finished stepping.
        yield return Expect("the driver released the walk key", () => !WalkKeyHeld(walkKey), 10f);
        if (failed) { Finish(); yield break; }
        var walked = Vector2.Distance(crate.transform.position, beforeWalk);
        Shot("09-after-walk-follow");
        Log("after walk: crateMoved=" + walked.ToString("F2") + " " + CrateState());

        // ============================================================ 5. right button reels it in
        // The pull target sits on the line from the crate to the player, 1.2m short of the player: aiming at
        // the player's own position reels the solid crate onto the player and shoves it (run 6 pushed the
        // player 13m down the corridor), which measures the fixture instead of the drag.
        float pullDistance = Vector2.Distance(crate.transform.position, hands.transform.position);
        Vector2 pullTarget = Vector2.Lerp(crate.transform.position, hands.transform.position,
            1f - 1.2f / Mathf.Max(0.01f, pullDistance));
        var pullPoint = OsPoint(pullTarget);
        float beforePull = pullDistance;
        PublishPlan("rmb-down", pullPoint, null,
            "hold the right button with the cursor between crate and player: pull the crate toward the cursor", "10-during-rmb-pull");
        yield return Expect("holding the right button pulled the crate toward the cursor, still on the leash",
            () => drag.IsDragging &&
                  Vector2.Distance(crate.transform.position, hands.transform.position) < beforePull - .3f &&
                  Vector2.Distance(crate.transform.position, hands.transform.position) <= drag.MaxDragRadius + .25f, 20f);
        if (failed) { Shot("10-pull-failed"); Finish(); yield break; }
        float afterPull = Vector2.Distance(crate.transform.position, hands.transform.position);
        Log("after pull: distanceToCursor " + beforePull.ToString("F2") + " -> " + afterPull.ToString("F2"));

        // plan.json is a single slot, so the next step may only be published once this one is provably
        // delivered: the held button itself is the proof, and the drag surviving it is the clause.
        yield return Expect("the right button was really held down by the driver",
            () => Mouse.current != null && Mouse.current.rightButton.isPressed, 10f);
        if (failed) { Shot("10-pull-failed"); Finish(); yield break; }
        PublishPlan("rmb-up", Vector2.zero, null, "release the right button (keep dragging)", null);
        yield return Expect("the driver released the right button",
            () => Mouse.current == null || !Mouse.current.rightButton.isPressed, 10f);
        if (failed) { Finish(); yield break; }
        yield return Expect("the drag survived the right-button release", () => drag.IsDragging, 5f);

        // ============================================================ 6. release: same crate, same world
        var droppedAt = crate.transform.position;
        int occupiedBefore = OccupiedBagCells(crateBag);
        PublishPlan("ctrl-up", Vector2.zero, null, "release Ctrl: the crate must be dropped where it stands", "11-final-drop");
        yield return Expect("releasing Ctrl ended the crate drag", () => !drag.IsDragging, 15f);
        if (failed) { Finish(); yield break; }
        yield return Settle(5);

        Expect("the crate is still in the world where it was dropped",
            crate != null && crate.gameObject.activeInHierarchy &&
            Vector2.Distance(crate.transform.position, droppedAt) < .01f);
        Expect("the crate was never parented to a hand",
            crate.transform.parent == null || crate.transform.parent.GetComponent<HandSystem>() == null);
        Expect("the crate is still not an ItemComponent and was never picked up",
            crate.GetComponent<ItemComponent>() == null && ActiveHandEmpty() && !InEitherHand(bottle));
        Expect("the crate's grid contents survived the whole drag cycle",
            OccupiedBagCells(crateBag) == occupiedBefore && BagContains(crateBag, witness) == (witness != null));

        // The crate has moved, so the interaction test has to use its new screen position.
        var cratePointAfter = OsPoint(crateCollider.bounds.center);
        PublishPlan("click", cratePointAfter, null,
            "plain left click the crate at its new position: normal interaction must still work",
            "12-crate-interact-after-drag");
        yield return Expect("plain click still opens the crate after the whole drag cycle", () => cratePanel.enabled, 20f);
        if (failed) { Finish(); yield break; }
        PublishPlan("click", cratePointAfter, null, "plain left click again (close the storage UI)", null);
        yield return Expect("the crate closes again after the drag cycle", () => !cratePanel.enabled, 20f);
        Log("final: " + CrateState());

        Finish();
    }

    void Finish()
    {
        Shot("13-final");
        Debug.Log("[CRATE_DRAG] final state: " + CrateState());
        Debug.Log("[CRATE_DRAG] gui events seen by observer: " + guiEventsLogged);
        PublishDone(!failed);
        Debug.Log(failed ? "CRATE_DRAG_PLAYTEST_FAIL" : "CRATE_DRAG_PLAYTEST_PASS");
        Application.Quit(failed ? 1 : 0);
    }
}
