using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryTransferTest : MonoBehaviour
{
    HandSystem hands;
    ItemComponent bottle;
    bool ok = true;

    public void Configure(HandSystem handSystem, ItemComponent item)
    {
        hands = handSystem;
        bottle = item;
    }

    IEnumerator Start()
    {
        yield return null;
        var ui = Object.FindFirstObjectByType<HandUI>();
        var inventory = Object.FindFirstObjectByType<InventorySandbox>();
        Check(ui != null && ui.LeftSlot != null && ui.RightSlot != null, "hand slots bound");
        Check(inventory != null && inventory.bag != null, "inventory bound");
        var satchel = FindItem(inventory.bag, "Basic satchel");
        var statusBar = GameObject.Find("LeftHandStatusBar");
        var statusEndCap = GameObject.Find("StatusLeftEndCap");
        Check(ui.BackpackSlot != null && ui.BackpackSlot.sprite != null && !ui.BackpackSlot.raycastTarget,
            "empty backpack equipment slot is visible and non-interactive");
        Canvas.ForceUpdateCanvases();
        var backpackBounds = ScreenRect(ui.BackpackSlot.rectTransform);
        var statusBodyBounds = statusBar == null ? default : ScreenRect((RectTransform)statusBar.transform);
        var statusCapBounds = statusEndCap == null ? default : ScreenRect((RectTransform)statusEndCap.transform);
        var statusVisibleBounds = statusBar == null || statusEndCap == null
            ? default
            : Rect.MinMaxRect(Mathf.Min(statusBodyBounds.xMin, statusCapBounds.xMin),
                Mathf.Min(statusBodyBounds.yMin, statusCapBounds.yMin),
                Mathf.Max(statusBodyBounds.xMax, statusCapBounds.xMax),
                Mathf.Max(statusBodyBounds.yMax, statusCapBounds.yMax));
        var leftHandBounds = ScreenRect(ui.LeftSlot.rectTransform);
        var backpackGap = statusVisibleBounds.xMin - backpackBounds.xMax;
        var backpackCenterOffset = Mathf.Abs(backpackBounds.center.y - leftHandBounds.center.y);
        Debug.Log($"HUD_BOUNDS {Screen.width}x{Screen.height}: backpack={backpackBounds}, statusBody={statusBodyBounds}, statusCap={statusCapBounds}, statusVisible={statusVisibleBounds}, leftHand={leftHandBounds}, visibleGap={backpackGap:F1}px, centerOffset={backpackCenterOffset:F1}px, bottomClearance={backpackBounds.yMin:F1}px");
        Check(statusBar != null && statusEndCap != null && backpackGap >= 8f,
            "backpack slot has at least 8px clearance from the status bar's visible end cap");
        Check(backpackCenterOffset <= .1f,
            "backpack slot is vertically center-aligned with the hand slots");
        Check(backpackBounds.yMin >= 4f,
            "backpack slot is not clipped by the bottom of the screen");
        Check(satchel != null && satchel.Definition.ItemIcon != null && CountItems(inventory.bag) == 1,
            "storage starts with exactly one basic satchel while the backpack slot stays empty");
        var renderer = bottle.GetComponent<SpriteRenderer>();
        Check(bottle.Definition != null && bottle.Definition.ItemName == "Water bottle"
            && bottle.Definition.ItemIcon != null && renderer != null
            && bottle.Definition.ItemIcon == renderer.sprite,
            "world bottle uses the named water-bottle inventory icon");
        Check(hands.TryPickup(bottle), "water bottle picked up in left hand");

        Check(Click(ui.RightSlot), "right-hand UI slot receives pointer click");
        Check(hands.CurrentHandSlot == HandSystem.HandSlot.Right
            && hands.HeldItem(HandSystem.HandSlot.Left) == null
            && hands.HeldItem(HandSystem.HandSlot.Right) == bottle,
            "left-to-right transfer moves one item");
        Check(ui.LeftItemIcon != null && !ui.LeftItemIcon.enabled
            && ui.RightItemIcon != null && ui.RightItemIcon.enabled,
            "hand icons refresh after transfer");
        yield return Capture("hand-transfer-right.png");

        var prefab = Resources.Load<GameObject>("WorldItems/WaterBottle");
        var second = Instantiate(prefab, hands.transform.position + Vector3.right * .8f, Quaternion.identity).GetComponent<ItemComponent>();
        hands.SwapHands();
        Physics2D.SyncTransforms();
        Check(hands.TryPickup(second), "second item picked up in empty left hand");
        var bottleParent = bottle.transform.parent;
        var secondParent = second.transform.parent;
        var bottlePosition = bottle.transform.position;
        var secondPosition = second.transform.position;
        Check(Click(ui.RightSlot), "occupied right-hand UI slot receives pointer click");
        Check(hands.CurrentHandSlot == HandSystem.HandSlot.Right
            && hands.HeldItem(HandSystem.HandSlot.Left) == bottle
            && hands.HeldItem(HandSystem.HandSlot.Right) == second,
            "occupied left-to-right click swaps item identities and selects right hand");
        Check(bottle.transform.parent == bottleParent && second.transform.parent == secondParent
            && bottle.transform.position == bottlePosition && second.transform.position == secondPosition
            && !bottle.gameObject.activeSelf && !second.gameObject.activeSelf,
            "swap preserves held item transforms, attachments and active state");
        Check(ui.LeftItemIcon != null && ui.LeftItemIcon.enabled
            && ui.RightItemIcon != null && ui.RightItemIcon.enabled,
            "both hand icons remain visible after occupied swap");
        yield return Capture("hand-swap-right.png");

        Check(Click(ui.LeftSlot), "occupied left-hand UI slot receives pointer click");
        Check(hands.CurrentHandSlot == HandSystem.HandSlot.Left
            && hands.HeldItem(HandSystem.HandSlot.Left) == second
            && hands.HeldItem(HandSystem.HandSlot.Right) == bottle,
            "occupied right-to-left click swaps item identities and selects left hand");
        Check(Click(ui.LeftSlot)
            && hands.CurrentHandSlot == HandSystem.HandSlot.Left
            && hands.HeldItem(HandSystem.HandSlot.Left) == second
            && hands.HeldItem(HandSystem.HandSlot.Right) == bottle,
            "clicking active hand is a no-op");

        Check(inventory.TryUseBag(), "bag click action stores active item");
        Check(hands.HeldItem(HandSystem.HandSlot.Left) == null && second.Container == inventory.bag
            && CountItems(inventory.bag) == 2 && Contains(inventory.bag, satchel),
            "stored item leaves hand without duplication and occupies storage");
        Check(inventory.bag.Remove(second), "test item can be removed before the actual bottle storage check");
        Destroy(second.gameObject);
        Check(Click(ui.RightSlot) && hands.CurrentHandSlot == HandSystem.HandSlot.Right
            && hands.HeldItem(HandSystem.HandSlot.Right) == bottle,
            "empty hand click selects occupied hand without moving its item");
        Check(Click(ui.LeftSlot) && hands.HeldItem(HandSystem.HandSlot.Left) == bottle
            && hands.HeldItem(HandSystem.HandSlot.Right) == null,
            "right-to-left transfer is symmetric");

        var fullObject = new GameObject("Full inventory test");
        var fullBag = fullObject.AddComponent<InventoryContainer>();
        int placed = 0;
        while (true)
        {
            var filler = Instantiate(prefab).GetComponent<ItemComponent>();
            if (!fullBag.TryAutoPlace(filler)) { Destroy(filler.gameObject); break; }
            placed++;
        }
        Check(placed > 0 && !hands.TryStoreActiveItem(fullBag)
            && hands.HeldItem(HandSystem.HandSlot.Left) == bottle && bottle.Container == null,
            "full inventory failure preserves held item");
        Destroy(fullObject);

        Check(inventory.TryUseBag(), "water bottle stashes in available inventory");
        Check(hands.HeldItem(HandSystem.HandSlot.Left) == null && bottle.Container == inventory.bag
            && CountItems(inventory.bag) == 2 && Contains(inventory.bag, bottle) && Contains(inventory.bag, satchel)
            && bottle.Definition.ItemName == "Water bottle" && bottle.Definition.ItemIcon != null
            && ui.LeftItemIcon != null && !ui.LeftItemIcon.enabled,
            "same world bottle appears once in storage with its item data and clears the hand");
        Check(!inventory.TryUseBag() && inventory.isActiveAndEnabled,
            "empty-hand bag use leaves storage available");
        yield return Capture("inventory-stashed.png");

        Debug.Log(ok ? "INVENTORY_TRANSFER_PASS" : "INVENTORY_TRANSFER_FAIL");
        Application.Quit(ok ? 0 : 1);
    }

    int CountItems(InventoryContainer container)
    {
        var found = new HashSet<ItemComponent>();
        var dimensions = container.ContainerDimensions;
        for (int y = 0; y < dimensions.y; y++) for (int x = 0; x < dimensions.x; x++)
        {
            var item = container.GetItem(x, y);
            if (item != null) found.Add(item);
        }
        return found.Count;
    }

    bool Contains(InventoryContainer container, ItemComponent expected)
    {
        var dimensions = container.ContainerDimensions;
        for (int y = 0; y < dimensions.y; y++) for (int x = 0; x < dimensions.x; x++)
            if (container.GetItem(x, y) == expected) return true;
        return false;
    }

    ItemComponent FindItem(InventoryContainer container, string itemName)
    {
        var dimensions = container.ContainerDimensions;
        for (int y = 0; y < dimensions.y; y++) for (int x = 0; x < dimensions.x; x++)
        {
            var item = container.GetItem(x, y);
            if (item != null && item.Definition != null && item.Definition.ItemName == itemName) return item;
        }
        return null;
    }

    bool Click(Image slot)
    {
        if (slot == null || EventSystem.current == null) return false;
        Canvas.ForceUpdateCanvases();
        var position = RectTransformUtility.WorldToScreenPoint(null, slot.rectTransform.TransformPoint(slot.rectTransform.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, hits);
        foreach (var hit in hits)
        {
            if (hit.gameObject != slot.gameObject) continue;
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.pointerClickHandler);
            return true;
        }
        Debug.LogError($"No UI raycast hit for {slot.name} at {position}");
        return false;
    }

    Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        var topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
    }

    IEnumerator Capture(string fileName)
    {
        yield return new WaitForEndOfFrame();
        var directory = System.Environment.GetEnvironmentVariable("NTEXPEDITION_EVIDENCE_DIR");
        if (string.IsNullOrEmpty(directory)) yield break;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForSecondsRealtime(.5f);
        Check(File.Exists(path), "captured " + fileName);
    }

    void Check(bool result, string name)
    {
        Debug.Log((result ? "PASS: " : "FAIL: ") + name);
        ok &= result;
    }
}
