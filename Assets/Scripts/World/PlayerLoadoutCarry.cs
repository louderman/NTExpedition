using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Dan's rule: whatever the player is holding - or has stowed in the ship's inventory panel - comes with them
// through a portal hop. ScenePortal loads the destination with LoadSceneMode.Single, which destroys the
// outgoing scene and every item in it, so the loadout is snapshotted (definition + prefab + which hand +
// wielded state) before the load and rebuilt in the arriving scene. The same applies to an item the player is
// dragging (ItemDragSystem): it is in no hand and no container, so it is captured by CaptureDragged and
// re-attached to the arriving scene's drag system.
//
// Design choice: the carry is general (driven by the item's own ItemDefinition/prefab, not hard-coded to the
// accelerator). Held items are re-issued to the arriving player's hands; stowed items are re-placed in the
// destination's inventory container when that scene has one, otherwise they stay in transit and are restored
// on a later hop (a scene without an inventory panel cannot hold them without inventing a container).
public static class PlayerLoadoutCarry
{
    const string ItemFolder = "WorldItems";
    const string LogPrefix = "[PlayerLoadout]";

    struct Record
    {
        public string DefinitionName;
        public string PrefabPath;
        public bool FromLeftHand;
        public bool Wielded;
        public bool FromStorage;
        public bool FromDrag;
    }

    static readonly List<Record> inTransit = new List<Record>();
    static Dictionary<string, GameObject> prefabByDefinitionName;

    public static bool PendingCarry => inTransit.Count > 0;
    public static int PendingCarryCount => inTransit.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init() { SceneManager.sceneLoaded += OnSceneLoaded; }

    // ScenePortal calls this immediately before LoadSceneAsync: the outgoing scene still owns the items here.
    public static void Capture(HandSystem hands, InventoryContainer storage)
    {
        int added = 0;
        if (hands != null)
        {
            added += CaptureHeldItem(hands, HandSystem.HandSlot.Left);
            added += CaptureHeldItem(hands, HandSystem.HandSlot.Right);
        }
        if (storage != null) added += CaptureStorage(storage);
        Debug.Log($"{LogPrefix} captured {added} item(s); {inTransit.Count} in transit");
    }

    // Dan's rule extended to dragging: a dragged item is in no hand and no container, so Capture above cannot
    // see it. ScenePortal calls this right after Capture, while the item still belongs to the outgoing scene.
    public static void CaptureDragged(ItemDragSystem dragger)
    {
        var item = dragger != null ? dragger.DraggedItem : null;
        if (item == null || item.Definition == null) return;
        inTransit.Add(new Record
        {
            DefinitionName = item.Definition.name,
            PrefabPath = PrefabPathFor(item.Definition),
            FromDrag = true,
        });
        Debug.Log($"{LogPrefix} captured dragged {item.name}; {inTransit.Count} in transit");
    }

    static int CaptureHeldItem(HandSystem hands, HandSystem.HandSlot slot)
    {
        var item = hands.HeldItem(slot);
        if (item == null || item.Definition == null) return 0;
        inTransit.Add(new Record
        {
            DefinitionName = item.Definition.name,
            PrefabPath = PrefabPathFor(item.Definition),
            FromLeftHand = slot == HandSystem.HandSlot.Left,
            Wielded = hands.IsTwoHandWielded(item),
        });
        return 1;
    }

    static int CaptureStorage(InventoryContainer storage)
    {
        var seen = new HashSet<ItemComponent>();
        int added = 0;
        var dims = storage.ContainerDimensions;
        for (int x = 0; x < dims.x; x++)
        {
            for (int y = 0; y < dims.y; y++)
            {
                // A multi-cell item answers for each cell it covers; one record per instance.
                var item = storage.GetItem(x, y);
                if (item == null || item.Definition == null || !seen.Add(item)) continue;
                inTransit.Add(new Record
                {
                    DefinitionName = item.Definition.name,
                    PrefabPath = PrefabPathFor(item.Definition),
                    FromStorage = true,
                });
                added++;
            }
        }
        return added;
    }

    static string PrefabPathFor(ItemDefinition definition)
    {
        if (prefabByDefinitionName == null)
        {
            prefabByDefinitionName = new Dictionary<string, GameObject>();
            foreach (var candidate in Resources.LoadAll<GameObject>(ItemFolder))
            {
                var item = candidate.GetComponent<ItemComponent>();
                if (item != null && item.Definition != null) prefabByDefinitionName[item.Definition.name] = candidate;
            }
        }
        GameObject prefab;
        return prefabByDefinitionName.TryGetValue(definition.name, out prefab) ? ItemFolder + "/" + prefab.name : null;
    }

    // Runs when the destination scene is loaded (registered before its own AfterSceneLoad spawners).
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (inTransit.Count == 0) return;
        var hands = Object.FindFirstObjectByType<HandSystem>();
        if (hands == null)
        {
            Debug.LogWarning($"{LogPrefix} {inTransit.Count} item(s) still in transit: '{scene.name}' has no hand system");
            return;
        }
        var storage = Object.FindFirstObjectByType<InventoryContainer>();
        int restored = 0;
        var remaining = new List<Record>();
        var ordered = new List<Record>(inTransit);
        ordered.Sort((a, b) => b.Wielded.CompareTo(a.Wielded)); // wielded first so it takes the active hand
        foreach (var record in ordered)
        {
            var item = Instantiate(record, hands.transform.position);
            if (item == null) { remaining.Add(record); continue; }
            if (record.FromDrag)
            {
                // Nothing holds a dragged item in the arriving scene, so it goes straight back to that
                // scene's drag system; a scene without one simply keeps the item at the player's feet.
                item.transform.position = hands.transform.position;
                Physics2D.SyncTransforms();
                var dragger = hands.GetComponent<ItemDragSystem>();
                if (dragger == null) dragger = Object.FindFirstObjectByType<ItemDragSystem>();
                if (dragger == null || !dragger.BeginDrag(item))
                    Debug.LogWarning($"{LogPrefix} {item.name} could not be re-attached to a drag; left at the player's feet");
                restored++;
                continue;
            }
            if (record.FromStorage)
            {
                if (storage != null && storage.TryAutoPlace(item)) { restored++; continue; }
                Object.Destroy(item.gameObject);
                remaining.Add(record);
                continue;
            }
            item.transform.position = hands.transform.position;
            Physics2D.SyncTransforms();
            if (!hands.TryPickup(item))
            {
                // Both hands reserved or combat-locked: leave it in the world so the player can grab it.
                Debug.LogWarning($"{LogPrefix} {item.name} could not be re-issued to a hand; left at the player's feet");
                restored++;
                continue;
            }
            if (record.Wielded && item == hands.HeldItem(hands.CurrentHandSlot)) hands.TryWieldActiveItem(item);
            restored++;
        }
        inTransit.Clear();
        inTransit.AddRange(remaining);
        Debug.Log($"{LogPrefix} restored {restored} item(s) into '{scene.name}'; {remaining.Count} still in transit");
    }

    static ItemComponent Instantiate(Record record, Vector3 position)
    {
        GameObject instance = null;
        var prefab = string.IsNullOrEmpty(record.PrefabPath) ? null : Resources.Load<GameObject>(record.PrefabPath);
        if (prefab != null) instance = Object.Instantiate(prefab, position, Quaternion.identity);
        if (instance == null)
        {
            var definition = FindDefinition(record.DefinitionName);
            if (definition == null) return null;
            instance = new GameObject(record.DefinitionName);
            instance.transform.position = position;
            instance.AddComponent<ItemComponent>().Initialize(definition);
        }
        return instance.GetComponent<ItemComponent>();
    }

    static ItemDefinition FindDefinition(string definitionName)
    {
        foreach (var definition in Resources.LoadAll<ItemDefinition>(ItemFolder))
            if (definition != null && definition.name == definitionName) return definition;
        return null;
    }
}
