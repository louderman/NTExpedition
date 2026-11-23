using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

// Kanban card t_f8860ae1 observer (opt-in: --crate-loop-playtest).
//
// This component does NOT perform any of the four acceptance actions. It only:
//   1. builds a deterministic fixture (player beside the crate, world bottle in reach),
//   2. publishes the screen coordinates of the next intended click to
//      <NTEXPEDITION_EVIDENCE_DIR>/plan.json, one phase at a time,
//   3. waits for real OS-level mouse/keyboard input (xdotool on the isolated display) to arrive,
//   4. records the resulting inventory state and logs PASS/FAIL for each acceptance clause.
// The input itself is genuine: the same X11 pointer/keyboard events a human would generate.
public class CrateLoopPlaytest : MonoBehaviour
{
    const float Cell = 32f;        // InventorySandbox.Cell
    const float PanelPadX = 32f;   // InventorySandbox grid origin: panel.x + 32
    const float PanelPadY = 24f;   // InventorySandbox grid origin: panel.y + 24
    const float DefaultPanelX = 24f;
    const float DefaultPanelY = 70f;

    HandSystem hands;
    ItemComponent bottle;
    StorageCrate crate;
    InventorySandbox cratePanel;
    InventorySandbox otherPanel;
    InventoryContainer crateBag;
    Collider2D crateCollider;
    Camera cam;
    string evid = ".";
    int phase;
    int bottleInstancesAtSetup;
    bool failed;
    int guiEventsLogged;

    public void Configure(HandSystem h, ItemComponent b) { hands = h; bottle = b; }

    void Awake()
    {
        evid = Environment.GetEnvironmentVariable("NTEXPEDITION_EVIDENCE_DIR");
        if (string.IsNullOrEmpty(evid)) evid = Directory.GetCurrentDirectory();
        Directory.CreateDirectory(evid);
    }

    // ------------------------------------------------------------------ reflection helpers
    static FieldInfo Field(Type type, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    static ItemComponent Dragging(InventorySandbox panel)
    {
        var f = Field(typeof(InventorySandbox), "dragging");
        return f == null || panel == null ? null : (ItemComponent)f.GetValue(panel);
    }

    static int DragRotation(InventorySandbox panel)
    {
        var f = Field(typeof(InventorySandbox), "dragRotation");
        return f == null || panel == null ? -1 : (int)f.GetValue(panel);
    }

    static bool CenterOnScreen(InventorySandbox panel)
    {
        var f = Field(typeof(InventorySandbox), "centerOnScreen");
        return f != null && panel != null && (bool)f.GetValue(panel);
    }

    // ------------------------------------------------------------------ evidence plumbing
    void Log(string message) { Debug.Log("[CRATE_LOOP] " + message); }

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

    // Screen point (bottom-left origin, as Unity reports it) -> xdotool coordinates (top-left origin).
    Vector2 OsPoint(Vector3 world)
    {
        var screen = cam.WorldToScreenPoint(world);
        return new Vector2(screen.x, Screen.height - screen.y);
    }

    Rect PanelRect(InventorySandbox panel)
    {
        var dimensions = panel.bag.ContainerDimensions;
        float width = dimensions.x * Cell + 32f;   // matches InventorySandbox.GetPanelRect
        float height = dimensions.y * Cell + 24f;
        if (CenterOnScreen(panel))
            return new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
        return new Rect(DefaultPanelX, DefaultPanelY, width, height);
    }

    // Centre of a storage cell, in xdotool coordinates (GUI y grows downwards, same as xdotool).
    Vector2 CellPoint(InventorySandbox panel, int cellX, int cellY)
    {
        var panelRect = PanelRect(panel);
        return new Vector2(panelRect.x + PanelPadX + cellX * Cell + Cell / 2f,
                           panelRect.y + PanelPadY + cellY * Cell + Cell / 2f);
    }

    void PublishPlan(string kind, Vector2 point, string key, string label)
    {
        phase++;
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"phase\": {0},\n", phase);
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"kind\": \"{0}\",\n", kind);
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"x\": {0:0},\n", point.x);
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"y\": {0:0},\n", point.y);
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"key\": \"{0}\",\n", key ?? "");
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"label\": \"{0}\"\n", label);
        sb.Append("}\n");
        var path = Path.Combine(evid, "plan.json");
        var temp = path + ".tmp";
        File.WriteAllText(temp, sb.ToString());
        if (File.Exists(path)) File.Delete(path);
        File.Move(temp, path);
        Log("plan phase " + phase + ": " + kind + " '" + label + "' at xdotool (" +
            point.x.ToString("0", CultureInfo.InvariantCulture) + "," +
            point.y.ToString("0", CultureInfo.InvariantCulture) + ")");
    }

    void PublishDone(bool ok)
    {
        var sb = new StringBuilder();
        sb.Append("{\n  \"phase\": 99,\n  \"done\": true,\n");
        sb.AppendFormat(CultureInfo.InvariantCulture, "  \"ok\": {0}\n", ok ? "true" : "false");
        sb.Append("}\n");
        var path = Path.Combine(evid, "plan.json");
        File.WriteAllText(path + ".tmp", sb.ToString());
        if (File.Exists(path)) File.Delete(path);
        File.Move(path + ".tmp", path);
    }

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
        var dimensions = container.ContainerDimensions;
        for (int y = 0; y < dimensions.y; y++)
            for (int x = 0; x < dimensions.x; x++)
                if (container.GetItem(x, y) == item) count++;
        return count;
    }

    int BottleInstanceCount()
    {
        int count = 0;
        foreach (var item in UnityEngine.Object.FindObjectsByType<ItemComponent>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (item != null && item.Definition != null && item.Definition.ItemName == "Water bottle") count++;
        return count;
    }

    string State()
    {
        var held = hands == null ? null : hands.HeldItem(hands.CurrentHandSlot);
        var other = hands == null ? null : hands.HeldItem(hands.OtherHand(hands.CurrentHandSlot));
        return "hand(" + (hands == null ? "?" : hands.CurrentHandSlot.ToString()) + ")=" +
               (held == null ? "empty" : held.Definition.ItemName) +
               " otherHand=" + (other == null ? "empty" : other.Definition.ItemName) +
               " cratePanelEnabled=" + (cratePanel != null && cratePanel.enabled) +
               " crateBagBottleCells=" + CountBagCells(crateBag, bottle) +
               " interactionBottleCells=" + (otherPanel != null ? CountBagCells(otherPanel.bag, bottle) : -1) +
               " bottleActive=" + (bottle != null && bottle.gameObject.activeInHierarchy) +
               " rotation=" + (bottle != null ? bottle.Rotation : -1) +
               " cell=" + (bottle != null ? bottle.GridPosition.ToString() : "-") +
               " dragging=" + (Dragging(cratePanel) == null ? "none" : Dragging(cratePanel).name) +
               " otherDragging=" + (otherPanel != null && Dragging(otherPanel) != null ? Dragging(otherPanel).name : "none");
    }

    // Diagostic only: records the raw IMGUI events this observer sees, so the run log proves
    // whether the real OS pointer/keyboard events reached the game's IMGUI layer at all.
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
        crateBag = cratePanel == null ? null : cratePanel.bag;
        bottleInstancesAtSetup = BottleInstanceCount();

        Log("setup: screen=" + Screen.width + "x" + Screen.height +
            " camera=" + (cam == null ? "MISSING" : (cam.orthographic ? "ortho " + cam.orthographicSize : "persp")) +
            " crate=" + (crate == null ? "MISSING" : crate.name) +
            " cratePanel=" + (cratePanel == null ? "MISSING" : cratePanel.name) +
            " cratePanels=" + panels.Length +
            " crateBag=" + (crateBag == null ? "-" : crateBag.ContainerDimensions.ToString()) +
            " otherPanel=" + (otherPanel == null ? "none" : otherPanel.name + " " + otherPanel.bag.ContainerDimensions) +
            " bottleInstances=" + bottleInstancesAtSetup);

        if (cam == null || crate == null || cratePanel == null || crateBag == null || crateCollider == null ||
            hands == null || bottle == null)
        {
            Fail("scene is missing one of: camera/crate/crate panel/crate container/player hands/world bottle");
            Finish();
            yield break;
        }

        // ---- fixture (setup only, no acceptance action): player beside the crate, bottle in reach.
        var cratePosition = crate.transform.position;
        hands.transform.position = cratePosition + new Vector3(-1.35f, 0f, 0f);
        bottle.transform.position = hands.transform.position + new Vector3(0.75f, -0.55f, 0f);
        Physics2D.SyncTransforms();
        yield return Settle(3);   // camera follows in LateUpdate

        // ---- geometry self-check: our panel maths must agree with the game's own hit test.
        var crateRect = PanelRect(cratePanel);
        var gridCentre = CellPoint(cratePanel, 0, 0);
        var insideGrid = InventorySandbox.PointerOverStorage(new Vector2(gridCentre.x, Screen.height - gridCentre.y));
        var outsidePoint = new Vector2(Screen.width - 100f, 100f);
        var outsideReported = InventorySandbox.PointerOverStorage(new Vector2(outsidePoint.x, Screen.height - outsidePoint.y));
        Log("geometry: cratePanelRect(gui)=" + crateRect + " otherPanelRect(gui)=" +
            (otherPanel != null ? PanelRect(otherPanel).ToString() : "-") +
            " gridCentre=" + gridCentre.ToString("0") + " PointerOverStorage(gridCentre)=" + insideGrid +
            " PointerOverStorage(outside)=" + outsideReported);
        Expect("panel geometry agrees with InventorySandbox.PointerOverStorage", insideGrid && !outsideReported);

        var bottlePoint = OsPoint(bottle.transform.position);
        var cratePoint = OsPoint(crateCollider.bounds.center);
        bool crateOverlap = crateCollider.OverlapPoint(crateCollider.bounds.center);
        Log("fixture: bottle xdotool=" + bottlePoint.ToString("0") + " crate xdotool=" + cratePoint.ToString("0") +
            " crateOverlapPoint=" + crateOverlap + " inRange=" +
            hands.HoverInRange + " playerCrateDistance=" +
            Vector2.Distance(hands.transform.position, crateCollider.ClosestPoint(hands.transform.position)).ToString("0.00"));
        Expect("world bottle is clear of every storage panel",
            !InventorySandbox.PointerOverStorage(new Vector2(bottlePoint.x, Screen.height - bottlePoint.y)));
        Expect("world crate is clear of every storage panel",
            !InventorySandbox.PointerOverStorage(new Vector2(cratePoint.x, Screen.height - cratePoint.y)));
        Expect("player is inside crate interaction range",
            Vector2.Distance(hands.transform.position, crateCollider.ClosestPoint(hands.transform.position)) <= 2f);
        if (failed) { Finish(); yield break; }

        // ---- the player starts the scene holding the loadout weapon, so free the active hand with
        // the game's own hand-swap key before picking anything up from the world.
        var activeItem = hands.HeldItem(hands.CurrentHandSlot);
        var otherHandItem = hands.HeldItem(hands.OtherHand(hands.CurrentHandSlot));
        Log("hands at start: slot=" + hands.CurrentHandSlot +
            " active=" + (activeItem == null ? "empty" : activeItem.Definition.ItemName) +
            " other=" + (otherHandItem == null ? "empty" : otherHandItem.Definition.ItemName));
        if (activeItem != null)
        {
            if (otherHandItem != null)
            {
                Fail("both hands are occupied before the pickup (" + activeItem.Definition.ItemName + " / " +
                     otherHandItem.Definition.ItemName + "), so a world pickup cannot be demonstrated");
                Finish();
                yield break;
            }
            PublishPlan("key", Vector2.zero, "x", "press X to swap the active hand (the other hand is empty)");
            yield return Expect("active hand is now empty after the swap-hands key",
                () => hands.HeldItem(hands.CurrentHandSlot) == null, 15f);
            if (failed) { Finish(); yield break; }
            Pass("starting loadout weapon moved out of the active hand with the game's swap-hands key (x)");
            Log("after swap: " + State());
        }

        // ============================================================ 1. pick up a world item
        Shot("01-fixture-before-pickup");
        PublishPlan("click", bottlePoint, null, "left click the world water bottle");
        yield return Expect("bottle picked up into the active hand",
            () => hands.HeldItem(hands.CurrentHandSlot) == bottle && !bottle.gameObject.activeInHierarchy, 25f);
        if (failed) { Shot("01-pickup-failed"); Finish(); yield break; }
        Shot("02-after-pickup");
        Log("after pickup: " + State());

        // ============================================================ 2. open the crate
        PublishPlan("click", cratePoint, null, "left click the crate (opens storage)");
        yield return Expect("crate storage panel is open", () => cratePanel.enabled, 20f);
        if (failed) { Finish(); yield break; }
        Shot("03-after-crate-open");
        Log("after crate open: " + State());

        // ============================================================ 3. put the item into the crate grid
        var storePoint = CellPoint(cratePanel, crateBag.ContainerDimensions.x - 2, 0);
        PublishPlan("click", storePoint, null, "left click the crate storage panel (stores the held item)");
        yield return Expect("water bottle is stored in the crate container",
            () => BagContains(crateBag, bottle) && hands.HeldItem(hands.CurrentHandSlot) == null, 20f);
        if (failed) { Finish(); yield break; }
        Shot("04-after-store");
        var storedCell = bottle.GridPosition;
        Log("after store: " + State() + " autoPlacedCell=" + storedCell);

        // ============================================================ 4. drag + rotate in the UI
        PublishPlan("mousedown", CellPoint(cratePanel, storedCell.x, storedCell.y), null,
            "press and hold the stored bottle in the crate grid");
        yield return Expect("crate panel is dragging the water bottle",
            () => Dragging(cratePanel) == bottle, 15f);
        if (failed) { Finish(); yield break; }

        PublishPlan("key", Vector2.zero, "r", "press R to rotate the dragged item");
        yield return Expect("drag rotation advanced to 1", () => DragRotation(cratePanel) == 1, 15f);
        if (failed) { Finish(); yield break; }
        Shot("05-drag-rotated");

        // ============================================================ 5. invalid placement must not lose or duplicate
        var invalidPoint = CellPoint(cratePanel, crateBag.ContainerDimensions.x - 1, 0);
        PublishPlan("mouseup", invalidPoint, null,
            "release on the right-hand edge cell (out of bounds when rotated): invalid placement");
        yield return Expect("drag ended after the invalid release", () => Dragging(cratePanel) == null, 15f);
        if (failed) { Finish(); yield break; }
        Shot("06-after-invalid-drop");
        Log("after invalid drop: " + State());
        Expect("invalid placement left the item at its original cell and rotation",
            bottle.GridPosition == storedCell && bottle.Rotation == 0 && BagContains(crateBag, bottle));
        Expect("invalid placement kept exactly one copy of the item in the crate",
            CountBagCells(crateBag, bottle) == bottle.Definition.GetFootprint().Length && BottleInstanceCount() == bottleInstancesAtSetup);
        Expect("invalid placement kept the hand empty",
            hands.HeldItem(hands.CurrentHandSlot) == null);

        // ============================================================ 6. valid rotated placement
        PublishPlan("mousedown", CellPoint(cratePanel, storedCell.x, storedCell.y), null,
            "press and hold the stored bottle again");
        yield return Expect("crate panel is dragging the water bottle again",
            () => Dragging(cratePanel) == bottle, 15f);
        if (failed) { Finish(); yield break; }

        PublishPlan("key", Vector2.zero, "r", "press R to rotate the dragged item again");
        yield return Expect("drag rotation advanced to 1 again", () => DragRotation(cratePanel) == 1, 15f);
        if (failed) { Finish(); yield break; }

        var targetCell = new Vector2Int(crateBag.ContainerDimensions.x - 2, 0);
        PublishPlan("mouseup", CellPoint(cratePanel, targetCell.x, targetCell.y), null,
            "release on a free cell: rotated placement into the crate");
        yield return Expect("bottle is placed rotated in the crate",
            () => Dragging(cratePanel) == null && bottle.GridPosition == targetCell && bottle.Rotation == 1, 15f);
        if (failed) { Finish(); yield break; }
        Shot("07-after-rotated-placement");
        Log("after rotated placement: " + State());
        Expect("rotated placement occupies both cells of the item footprint",
            crateBag.GetItem(targetCell.x, targetCell.y) == bottle &&
            crateBag.GetItem(targetCell.x + 1, targetCell.y) == bottle);

        // ============================================================ 7. retrieve the same item into a hand
        PublishPlan("mousedown", CellPoint(cratePanel, targetCell.x, targetCell.y), null,
            "press and hold the placed bottle in the crate grid");
        yield return Expect("crate panel is dragging the placed bottle",
            () => Dragging(cratePanel) == bottle, 15f);
        if (failed) { Finish(); yield break; }

        PublishPlan("mouseup", outsidePoint, null,
            "release outside the storage panel: the item goes back into the player's hand");
        yield return Expect("water bottle is back in the player's hand",
            () => hands.HeldItem(hands.CurrentHandSlot) == bottle, 15f);
        if (failed) { Finish(); yield break; }
        Shot("08-after-retrieve");
        Log("after retrieve: " + State());
        Expect("retrieved bottle is the very same instance and is no longer in the crate",
            !bottle.gameObject.activeInHierarchy && !BagContains(crateBag, bottle) &&
            bottle.transform.parent == hands.transform);
        Expect("retrieve removed every cell of the item from the crate",
            CountBagCells(crateBag, bottle) == 0 && BottleInstanceCount() == bottleInstancesAtSetup);

        Finish();
    }

    void Finish()
    {
        Shot("09-final");
        Debug.Log("[CRATE_LOOP] final state: " + State());
        Debug.Log("[CRATE_LOOP] gui events seen by observer: " + guiEventsLogged);
        PublishDone(!failed);
        Debug.Log(failed ? "CRATE_LOOP_PLAYTEST_FAIL" : "CRATE_LOOP_PLAYTEST_PASS");
        Application.Quit(failed ? 1 : 0);
    }
}
