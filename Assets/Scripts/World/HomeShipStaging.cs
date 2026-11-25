using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Dan's rule (kanban t_685b948c): the HomeShip is ONE persistent travelling ship.
//
//  * It is lifted out of the authored HomeShip scene exactly once - the authored objects are REPARENTED under
//    a persistent root (DontDestroyOnLoad), which preserves every world position, so the hub looks and plays
//    exactly as before and nothing in the hub loses its references.
//  * On every exploration map it is parked in a temporary OFF-MAP staging area: the maps are not designed
//    around the ship yet, so it sits just outside the authored layout, reached through a small opening cut
//    into the map's own outer wall. The opening is a runtime Tilemap.SetTile(null) on the loaded scene (no
//    scene asset is edited) and the parking spot is derived from the live tilemaps + the ship's own colliders.
//  * Anything physically lying on the ship's deck - loose items dragged aboard, and the ship's storage crate
//    with its contents - travels with the ship, because those objects are parented to the ship root. Nothing
//    else is captured: an object that is not inside the cargo volume is left to the map's own rules.
public class HomeShipStaging : MonoBehaviour
{
    public static HomeShipStaging Instance { get; private set; }

    const string LogTag = "[HomeShip]";
    const string HubScene = "HomeShip";
    static readonly string[] NonMapScenes = { "Main Menu", "Loading" };

    // Authored HomeShip footprint that holds the ship's own art/collision (the hub's player, camera, UI and
    // test rigs all sit outside it or are excluded by component below).
    const float ShipBoxMinX = -3.5f, ShipBoxMaxX = 8.5f, ShipBoxMinY = -18.5f, ShipBoxMaxY = 7f;
    const string CargoAnchorName = "cargo";
    // The hub authors the entire ship body - hull, rooms, fixtures, the travel console - as children of one Grid
    // root. That root IS the ship: reparenting it wholesale preserves every world position and every reference.
    const string ShipContainerName = "Grid";

    public bool HasPersistentShip { get { return lifted && shipRoot != null; } }
    public Transform ShipRoot { get { return shipRoot; } }
    /// <summary>Runtime facts the verification harness reports (and a human can read in the log).</summary>
    public string LastStagedScene { get; private set; }
    public string LastStagingSummary { get; private set; }
    public int LastWalkCells { get; private set; } = -1;
    public int LastFootprintOverlaps { get; private set; } = -1;
    public int LastSuppressedCount { get; private set; }
    public int LiftedObjectCount { get; private set; }
    /// <summary>Tiles of open void between the map's wall opening and the staged ship's hull.</summary>
    public int LastVoidWalkCells { get; private set; } = -1;
    /// <summary>Wall tiles cut out of the map to make the doorway (runtime only).</summary>
    public int LastOpeningCells { get; private set; }
    /// <summary>Which side of the opening the ship was parked on: east / west / north / south.</summary>
    public string LastOutward { get; private set; }
    /// <summary>World centre of the wall tile that was opened, so the harness can walk back in through it.</summary>
    public Vector3 LastOpeningWorld { get; private set; }

    Transform shipRoot;
    Transform cargoAnchor;
    bool lifted;
    Vector3 hubAnchor;
    Bounds shipLocalBounds;
    float hullWestX, hullEastX, hullNorthY, hullSouthY;
    Rect cargoRect = new Rect(0f, 0f, 0f, 0f);
    readonly List<Bounds> hullColliders = new List<Bounds>();
    float nextCargoScan;
    int stagedMaps;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (Instance != null) return;
        var go = new GameObject("[HomeShipStaging]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<HomeShipStaging>();
        SceneManager.sceneLoaded += Instance.OnSceneLoaded;
        Debug.Log(LogTag + " traveller online (persistent HomeShip, off-map staging per exploration map)");
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == HubScene) EnterHub(scene);
        else if (IsExplorationMap(scene.name)) EnterMap(scene);
    }

    static bool IsExplorationMap(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        for (int i = 0; i < NonMapScenes.Length; i++) if (name == NonMapScenes[i]) return false;
        return true;
    }

    // ------------------------------------------------------------------ hub
    void EnterHub(Scene scene)
    {
        if (!lifted)
        {
            Lift(scene);
            hubAnchor = shipRoot.position;
        }
        else
        {
            // Returning home reloads the authored hub, which brings its own copy of the ship, the crate and the
            // console. Those are duplicates of the persistent ship now, so they are switched OFF - not destroyed,
            // so any scene reference to them stays valid - and the persistent ship takes their place.
            var fresh = CollectShipObjects(scene);
            for (int i = 0; i < fresh.Count; i++) if (fresh[i].activeSelf) fresh[i].SetActive(false);
            LastSuppressedCount = fresh.Count;
            Debug.Log(LogTag + " hub re-entry: suppressed " + fresh.Count +
                      " freshly authored ship object(s); the persistent ship stays (cargo aboard: " + AboardCount() + ")");
        }
        shipRoot.SetPositionAndRotation(hubAnchor, Quaternion.identity);
        Physics2D.SyncTransforms();
    }

    void Lift(Scene scene)
    {
        shipRoot = new GameObject("[HomeShip]").transform;
        DontDestroyOnLoad(shipRoot.gameObject);
        var roots = CollectShipObjects(scene);
        for (int i = 0; i < roots.Count; i++) roots[i].transform.SetParent(shipRoot, true);

        cargoAnchor = new GameObject(CargoAnchorName).transform;
        cargoAnchor.SetParent(shipRoot, false);
        Measure();
        lifted = true;
        // Count the whole lifted body (container + every child), not just the roots that were reparented.
        LiftedObjectCount = shipRoot.GetComponentsInChildren<Transform>(true).Length;

        Debug.Log(LogTag + " lifted " + roots.Count + " authored root(s) / " + LiftedObjectCount + " object(s) into the persistent ship; local bounds " +
                  shipLocalBounds.min.ToString("0.##") + ".." + shipLocalBounds.max.ToString("0.##") +
                  "; hull x " + hullWestX.ToString("0.##") + ".." + hullEastX.ToString("0.##") +
                  "; cargo rect " + RectText(cargoRect));
    }

    List<GameObject> CollectShipObjects(Scene scene)
    {
        var result = new List<GameObject>();
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var go = roots[i];
            if (go == null || go == gameObject || go.transform == shipRoot) continue;
            // The authored ship body is a single container root holding every hull/room/fixture object under it, so
            // it is taken whole and never filtered as hub-only: loose items sitting on its deck are cargo too.
            if (go.name == ShipContainerName) { result.Add(go); continue; }
            if (IsHubOnly(go) || !IsShipPart(go)) continue;
            result.Add(go);
        }
        return result;
    }

    static readonly string[] HubOnlyNames =
    {
        "Background", "EventSystem", "Expedition flow", "Global Light 2D", "Inventory test interface",
        "Lab5 collectibles", "Main Camera", "Player",
    };

    static bool IsHubOnly(GameObject go)
    {
        for (int i = 0; i < HubOnlyNames.Length; i++) if (go.name == HubOnlyNames[i]) return true;
        if (go.GetComponentInChildren<Camera>(true) != null) return true;
        if (go.GetComponentInChildren<Canvas>(true) != null) return true;
        if (go.GetComponentInChildren<PlayerMovement>(true) != null) return true;
        if (go.GetComponentInChildren<ItemComponent>(true) != null) return true;   // loose items are cargo, not hull
        if (go.GetComponentInChildren<HandSystem>(true) != null) return true;
        if (go.GetComponentInChildren<ItemDragSystem>(true) != null) return true;
        return false;
    }

    static bool IsShipPart(GameObject go)
    {
        if (go.GetComponentInChildren<StorageCrate>(true) != null) return true;      // the ship's storage crate
        if (go.GetComponentInChildren<ScenePortal>(true) != null) return true;       // the ship's travel console
        if (go.GetComponentInChildren<ProximityAirlock>(true) != null) return true;  // the ship's airlocks
        var p = go.transform.position;
        if (p.x < ShipBoxMinX || p.x > ShipBoxMaxX || p.y < ShipBoxMinY || p.y > ShipBoxMaxY) return false;
        if (go.GetComponentInChildren<SpriteRenderer>(true) != null) return true;
        if (go.GetComponentInChildren<Collider2D>(true) != null) return true;
        if (go.GetComponentInChildren<Tilemap>(true) != null) return true;
        return false;
    }

    static Bounds ToSpace(Bounds world, Transform space)
    {
        var a = space.InverseTransformPoint(world.min);
        var b = space.InverseTransformPoint(world.max);
        return new Bounds((a + b) * 0.5f,
            new Vector3(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y), 0f));
    }

    // Local row of the ship's own airlock on the facing hull (the authored way in) for each side.
    float doorWestRow, doorEastRow;
    bool hasWestDoor, hasEastDoor;

    void Measure()
    {
        hullColliders.Clear();
        var cs = shipRoot.GetComponentsInChildren<Collider2D>(true);
        var rs = shipRoot.GetComponentsInChildren<SpriteRenderer>(true);
        bool first = true;
        var bounds = new Bounds();
        bounds = Union(bounds, ref first, cs, true);
        bounds = Union(bounds, ref first, rs, false);
        shipLocalBounds = bounds;

        for (int i = 0; i < cs.Length; i++)
        {
            if (cs[i] == null || !cs[i].enabled) continue;
            hullColliders.Add(ToSpace(cs[i].bounds, shipRoot));
        }

        float westFringe = shipLocalBounds.min.x + 1.5f;
        float eastFringe = shipLocalBounds.max.x - 1.5f;
        hullWestX = hullEastX = 0f;
        first = true;
        for (int i = 0; i < hullColliders.Count; i++)
        {
            var b = hullColliders[i];
            if (b.max.x <= westFringe) { if (first) { hullWestX = b.center.x; first = false; } else hullWestX = Mathf.Max(hullWestX, b.center.x); }
        }
        first = true;
        for (int i = 0; i < hullColliders.Count; i++)
        {
            var b = hullColliders[i];
            if (b.min.x >= eastFringe) { if (first) { hullEastX = b.center.x; first = false; } else hullEastX = Mathf.Min(hullEastX, b.center.x); }
        }
        if (hullWestX == 0f) hullWestX = shipLocalBounds.min.x + 0.5f;
        if (hullEastX == 0f) hullEastX = shipLocalBounds.max.x - 0.5f;

        // Same measurement for the top/bottom hulls: a map can also be left through its north or south wall.
        float southFringe = shipLocalBounds.min.y + 1.5f;
        float northFringe = shipLocalBounds.max.y - 1.5f;
        hullNorthY = hullSouthY = 0f;
        first = true;
        for (int i = 0; i < hullColliders.Count; i++)
        {
            var b = hullColliders[i];
            if (b.max.y <= southFringe) { if (first) { hullSouthY = b.center.y; first = false; } else hullSouthY = Mathf.Max(hullSouthY, b.center.y); }
        }
        first = true;
        for (int i = 0; i < hullColliders.Count; i++)
        {
            var b = hullColliders[i];
            if (b.min.y >= northFringe) { if (first) { hullNorthY = b.center.y; first = false; } else hullNorthY = Mathf.Min(hullNorthY, b.center.y); }
        }
        if (hullSouthY == 0f) hullSouthY = shipLocalBounds.min.y + 0.5f;
        if (hullNorthY == 0f) hullNorthY = shipLocalBounds.max.y - 0.5f;

        // The ship's own airlocks are the authored way in. Recording the facing-hull door row lets the map opening
        // line up with the shuttle's real doorway instead of a bare spot on its wall.
        hasWestDoor = hasEastDoor = false;
        doorWestRow = doorEastRow = shipLocalBounds.center.y;
        var airlocks = shipRoot.GetComponentsInChildren<ProximityAirlock>(true);
        for (int i = 0; i < airlocks.Length; i++)
        {
            if (airlocks[i] == null) continue;
            var doorLocal = ToSpace(new Bounds(airlocks[i].transform.position, Vector3.zero), shipRoot).center;
            if (doorLocal.x <= shipLocalBounds.min.x + 3f)
            {
                if (!hasWestDoor || Mathf.Abs(doorLocal.y - shipLocalBounds.center.y) < Mathf.Abs(doorWestRow - shipLocalBounds.center.y))
                    doorWestRow = doorLocal.y;
                hasWestDoor = true;
            }
            else if (doorLocal.x >= shipLocalBounds.max.x - 3f)
            {
                if (!hasEastDoor || Mathf.Abs(doorLocal.y - shipLocalBounds.center.y) < Mathf.Abs(doorEastRow - shipLocalBounds.center.y))
                    doorEastRow = doorLocal.y;
                hasEastDoor = true;
            }
        }

        // Cargo volume = the ship's interior, inset from the hull so doorway traffic and anything merely near
        // the ship is not swept up as cargo.
        float x0 = shipLocalBounds.min.x + 1f;
        float x1 = shipLocalBounds.max.x - 1f;
        float y0 = shipLocalBounds.min.y + 1f;
        float y1 = shipLocalBounds.max.y - 1f;
        cargoRect = new Rect(x0, y0, Mathf.Max(0.5f, x1 - x0), Mathf.Max(0.5f, y1 - y0));
    }

    Bounds Union(Bounds acc, ref bool first, Component[] comps, bool collider)
    {
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) continue;
            Bounds w;
            var c = comps[i] as Collider2D;
            if (collider)
            {
                if (c == null || !c.enabled) continue;
                w = c.bounds;
            }
            else
            {
                var r = comps[i] as SpriteRenderer;
                if (r == null || !r.enabled) continue;
                w = r.bounds;
            }
            var b = ToSpace(w, shipRoot);
            if (first) { acc = b; first = false; }
            else acc.Encapsulate(b);
        }
        return acc;
    }

    static string RectText(Rect r)
    {
        return "x " + r.xMin.ToString("0.##") + ".." + r.xMax.ToString("0.##") +
               " y " + r.yMin.ToString("0.##") + ".." + r.yMax.ToString("0.##");
    }

    // ------------------------------------------------------------------ exploration maps
    void EnterMap(Scene scene)
    {
        if (!HasPersistentShip)
        {
            Debug.LogError(LogTag + " '" + scene.name + "': no persistent ship to stage (the HomeShip hub never loaded)");
            return;
        }
        stagedMaps++;

        Tilemap wall, floor;
        FindMaps(scene, out wall, out floor);
        if (wall == null || floor == null)
        {
            Debug.LogError(LogTag + " '" + scene.name + "': wall/floor tilemaps not found; leaving the ship where it was");
            return;
        }

        var player = Object.FindFirstObjectByType<PlayerMovement>();
        var arrival = player != null ? (Vector3)player.transform.position : Vector3.zero;

        // Dan's marked docking walls win over the generic search: on those two maps the ship parks at the wall he
        // picked, hull flush against it, and the station gets three side-by-side airlock doors facing the
        // shuttle's own docking ports (see the dock table below).
        Placement best = new Placement();
        DockSpec? dock = DockFor(scene.name);
        bool docked = dock.HasValue && TryBuildDock(wall, floor, arrival, dock.Value, out best);
        if (!docked)
        {
            ClearDockState();
            if (!TryFindPlacement(wall, floor, arrival, out best))
            {
                // Never park on top of the authored map. The last resort is a spot provably beyond the map's own
                // tile bounds; it is reported as loudly as a failure because it means the search found nothing.
                best = OutsidePlacement(wall, floor, arrival);
                Debug.LogError(LogTag + " '" + scene.name + "': no outer-wall opening cleared the map; " + best.note);
            }
        }

        for (int i = 0; i < best.gapCells.Count; i++) wall.SetTile(best.gapCells[i], null);
        if (best.gapCells.Count > 0) wall.RefreshAllTiles();

        shipRoot.SetPositionAndRotation(best.root, Quaternion.identity);
        Physics2D.SyncTransforms();

        // Prove the boarding route rather than assume it: count the ship's own hull sitting on the strip between the
        // map opening and the deck, and clear that strip only when the authored doorway is still blocked.
        var corridor = CorridorLocalStrip(best, wall);
        int blockers = corridor.HasValue ? CollidersIn(shipRoot, corridor.Value, false) : -1;
        int cargoOnCorridor = corridor.HasValue ? CollidersIn(shipRoot, corridor.Value, true) : -1;
        int opened = blockers > 0 ? OpenBoardingAperture(best, wall) : 0;

        // Three side-by-side station airlock doors in the marked wall, lined up with the shuttle's docking ports.
        if (docked) BuildStationDocking(best, wall);

        int overlaps = CountAuthoredCellsInside(wall, Cells(wall), Cells(floor), best.root);
        var world = CargoRectWorld();
        LastStagedScene = scene.name;
        LastWalkCells = best.walkCells;
        LastVoidWalkCells = best.voidWalkCells;
        LastOpeningCells = best.gapCells.Count;
        LastOutward = best.outwardName;
        LastOpeningWorld = best.gapCells.Count > 0 ? wall.GetCellCenterWorld(best.gap) : Vector3.zero;
        LastFootprintOverlaps = overlaps;
        LastStagingSummary = "root " + best.root.ToString("0.##") + "; opening [" + CellsText(best.gapCells) +
                              "]; outward " + best.outwardName + "; walk " + (best.walkCells < 0 ? "n/a" : best.walkCells + " tile(s)") +
                              "; void walk " + best.voidWalkCells + " tile(s); connection row " + best.row.ToString("0.##") +
                              "; hull on the corridor " + blockers + (opened > 0 ? " (cleared " + opened + ")" : "") +
                              "; deck cargo on the corridor " + cargoOnCorridor +
                              "; overlaps " + overlaps + "; cargo " + RectText(world) + "; " + best.note +
                              (docked ? "; station docking: " + LastDockSummary : "");
        Debug.Log(LogTag + " MAP " + scene.name + " (staging #" + stagedMaps + "): ship root " + best.root.ToString("0.##") +
                  "; opening cells [" + CellsText(best.gapCells) + "]; outward " + best.outwardName +
                  "; walk from arrival " + (best.walkCells < 0 ? "n/a" : best.walkCells + " tile(s)") +
                  "; void walk from the opening to the deck " + best.voidWalkCells + " tile(s)" +
                  "; connection row " + best.row.ToString("0.##") + "; hull on the corridor " + blockers + (opened > 0 ? " (cleared " + opened + ")" : "") + "; deck cargo on the corridor " + cargoOnCorridor + "; authored cells inside the ship footprint: " + overlaps +
                  "; cargo volume (world) " + RectText(world) + "; " + best.note +
                  (docked ? "; station docking: " + LastDockSummary : ""));

        if (overlaps > 0)
            Debug.LogError(LogTag + " '" + scene.name + "': the ship footprint still overlaps " + overlaps +
                           " authored tile(s) - the staging spot is not clear");
    }

    // --------------------------------------------------------------------------- marked docking bays
    //
    // Dan marked one outer wall per arena (kanban t_6721b172): the west wall of the small side room that
    // projects west of the station in LargeExploreArena, and the east wall of the station room the ship already
    // parks beside in LargeBattleArena. On those two maps the generic "nearest reachable opening" search above is
    // bypassed: the ship docks at the marked wall with its hull flush against it, and three side-by-side airlock
    // doors are cut into that wall directly opposite the shuttle's own three hull docking ports.
    //
    // Everything here is runtime-only, exactly like the rest of the staging: no scene asset is edited and the
    // station bay lives in the map scene, so it disappears with the map. Door art and timing are copied from the
    // airlocks already wired in the hub, so the station doors and the shuttle doors are the same component with
    // the same upstream sprite set.

    enum DockSide { West, East }

    struct DockSpec
    {
        public string scene;
        public DockSide side;
    }

    static readonly DockSpec[] Docks =
    {
        new DockSpec { scene = "LargeExploreArena", side = DockSide.West },
        new DockSpec { scene = "LargeBattleArena", side = DockSide.East },
    };

    // Evidence surface for the harness: what the last docking built, door by door.
    public int LastDockDoorCells { get; private set; } = -1;
    public int LastDockShipDoors { get; private set; } = -1;
    public string LastDockSummary { get; private set; } = "";
    /// <summary>Exactly which door art the last docking applied, and how its two states were built.</summary>
    public string LastDoorArtSummary { get; private set; } = "";
    /// <summary>The original white hull-door sprite both halves of every pair show when closed.</summary>
    public Sprite WhiteClosedSprite { get { return whiteClosed; } }
    /// <summary>The open state built from that same art: aperture cleared, the door's own frame ring kept.</summary>
    public Sprite WhiteOpenSprite { get { return whiteOpen; } }
    public List<ProximityAirlock> StationDoors { get { return stationDoors; } }
    public List<ProximityAirlock> DockingDoors { get { return dockingDoors; } }
    readonly List<ProximityAirlock> stationDoors = new List<ProximityAirlock>();
    readonly List<ProximityAirlock> dockingDoors = new List<ProximityAirlock>();
    Sprite whiteClosed, whiteOpen;
    static Sprite cachedClosed, cachedOpen;

    static DockSpec? DockFor(string sceneName)
    {
        for (int i = 0; i < Docks.Length; i++) if (Docks[i].scene == sceneName) return Docks[i];
        return null;
    }

    void ClearDockState()
    {
        stationDoors.Clear();
        dockingDoors.Clear();
        LastDockDoorCells = -1;
        LastDockShipDoors = -1;
        LastDockSummary = "";
        LastDoorArtSummary = "";
    }

    // The marked wall, measured from the live tilemap instead of hard-coded: the outermost authored wall column on
    // that side, the three door rows centred on it, and the ship parked with its hull flush against the wall's
    // outer face. Returns false (so the generic search takes over) if the geometry is not what Dan marked.
    bool TryBuildDock(Tilemap wallMap, Tilemap floorMap, Vector3 arrival, DockSpec spec, out Placement placement)
    {
        placement = new Placement();
        var walls = Cells(wallMap);
        var floors = Cells(floorMap);
        if (walls.Count == 0 || floors.Count == 0)
        {
            Debug.LogError(LogTag + " marked dock: the map has no wall/floor tiles");
            return false;
        }

        bool west = spec.side == DockSide.West;
        var outward = west ? new Vector3Int(-1, 0, 0) : new Vector3Int(1, 0, 0);

        // Outer wall column of the marked side, and the rows that column really spans.
        int edge = west ? int.MaxValue : int.MinValue;
        foreach (var w in walls) edge = west ? Mathf.Min(edge, w.x) : Mathf.Max(edge, w.x);
        int lo = int.MaxValue, hi = int.MinValue;
        foreach (var w in walls) if (w.x == edge) { lo = Mathf.Min(lo, w.y); hi = Mathf.Max(hi, w.y); }
        if (lo > hi || hi - lo < 2)
        {
            Debug.LogError(LogTag + " marked dock: the " + (west ? "west" : "east") + " wall column x=" + edge +
                           " spans only y=" + lo + ".." + hi + " - no room for three side-by-side doorways");
            return false;
        }

        // Three side-by-side doorways, centred on the marked wall.
        int mid = Mathf.Clamp(Mathf.RoundToInt((lo + hi) * 0.5f), lo + 1, hi - 1);
        var cells = new List<Vector3Int>();
        for (int dy = -1; dy <= 1; dy++)
        {
            var c = new Vector3Int(edge, mid + dy, 0);
            if (walls.Contains(c)) cells.Add(c);
        }
        if (cells.Count != 3)
        {
            Debug.LogError(LogTag + " marked dock: only " + cells.Count + " of the three doorway cells at x=" + edge +
                           ", y=" + (mid - 1) + ".." + (mid + 1) + " are wall tiles");
            return false;
        }

        // The shuttle's own docking ports on the hull that faces the station: the middle of their rows is the row
        // the three station doorways line up with.
        float faceLine = west ? hullEastX : hullWestX;
        var portRows = new List<float>();
        var all = shipRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null || !t.name.StartsWith("Airlock")) continue;
            var local = ToSpace(new Bounds(t.position, Vector3.zero), shipRoot).center;
            if (Mathf.Abs(local.x - faceLine) <= 1f) portRows.Add(local.y);
        }
        if (portRows.Count < 3)
        {
            Debug.LogError(LogTag + " marked dock: " + portRows.Count + " docking port(s) on the shuttle's " +
                           (west ? "east" : "west") + " hull line x=" + faceLine.ToString("0.##") + "; docking generically");
            return false;
        }
        portRows.Sort();
        float portMid = portRows[portRows.Count / 2];

        var wallCentre = wallMap.GetCellCenterWorld(new Vector3Int(edge, mid, 0));
        float flushX = west
            ? wallCentre.x - 0.5f - (hullEastX + 0.5f)      // ship's east hull face on the wall's west face
            : wallCentre.x + 0.5f - (hullWestX - 0.5f);     // ship's west hull face on the wall's east face
        float rootY = (mid + 0.5f) - portMid;

        // Flush first, then step out only if an authored tile is in the way (it is not), so the two door lines stay
        // as close together as the geometry allows.
        float rootX = flushX, gap = 0f;
        for (gap = 0f; gap <= 6f; gap += 1f)
        {
            rootX = west ? flushX - gap : flushX + gap;
            if (CountAuthoredCellsInside(wallMap, walls, floors, new Vector3(rootX, rootY, 0f)) == 0) break;
        }
        var root = new Vector3(rootX, rootY, 0f);
        int overlaps = CountAuthoredCellsInside(wallMap, walls, floors, root);
        if (overlaps > 0)
        {
            Debug.LogError(LogTag + " marked dock: " + overlaps + " authored tile(s) still sit under the ship at the marked wall; docking generically");
            return false;
        }

        // Honest walkability, with the same BFS the generic search uses: the arrival cell to the floor cell beside
        // the middle doorway.
        int walk = -1;
        var snapped = SnapToFloor(wallMap.WorldToCell(arrival), floors, floorMap);
        if (snapped.HasValue)
        {
            var dist = Walkable(floors, snapped.Value);
            for (int i = 0; i < cells.Count; i++)
            {
                for (int d = 0; d < Dirs.Length; d++)
                {
                    int v;
                    if (floors.Contains(cells[i] + Dirs[d]) && dist.TryGetValue(cells[i] + Dirs[d], out v))
                        walk = walk < 0 ? v + 1 : Mathf.Min(walk, v + 1);
                }
            }
        }

        placement.root = root;
        placement.gap = new Vector3Int(edge, mid, 0);
        placement.gapCells = cells;
        placement.outward = outward;
        placement.outwardName = NameOutward(outward);
        placement.row = portMid;
        placement.walkCells = walk;
        placement.voidWalkCells = Mathf.RoundToInt(gap);
        placement.note = "docked at Dan's marked " + placement.outwardName + " station wall (column x=" + edge + "), hull " +
                         (gap <= 0f ? "flush against it" : gap.ToString("0.#") + " tile(s) off it") + ", three doorways [" +
                         CellsText(cells) + "] lined up with the shuttle's " + portRows.Count + " docking port(s) at hull row " +
                         portMid.ToString("0.##");
        return true;
    }

    // Builds the three station doors in the marked wall and gives the shuttle's three docking ports facing them the
    // same behaviour, so both halves of every airlock pair are the project's own proximity door, both show the
    // ORIGINAL white hull-door art the ship is authored with, and both switch instantly between the two states.
    void BuildStationDocking(Placement p, Tilemap wallMap)
    {
        ClearDockState();
        LastDockDoorCells = 0;
        LastDockShipDoors = 0;
        whiteClosed = null;
        whiteOpen = null;

        // Re-entering a map reloads its scene, but be defensive: never leave two bays in one scene.
        var stale = GameObject.Find("StationAirlockBay");
        if (stale != null) Destroy(stale);

        var reference = FindInteractionReference();
        var wallRenderer = wallMap.GetComponent<TilemapRenderer>();
        var bay = new GameObject("StationAirlockBay");

        // The shuttle's docking ports on the hull that faces the station. They are the second half of every pair AND
        // the source of the door art: a port already carries the original white hull-door sprite the ship is authored
        // with, so the station side can show that exact art instead of anything invented or borrowed from the hub.
        bool stationIsWest = p.outward.x < 0;
        float faceLine = stationIsWest ? hullEastX : hullWestX;
        string facing = stationIsWest ? "east" : "west";
        var ports = new List<Transform>();
        var all = shipRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null || !t.name.StartsWith("Airlock")) continue;
            var local = ToSpace(new Bounds(t.position, Vector3.zero), shipRoot).center;
            if (Mathf.Abs(local.x - faceLine) <= 1f) ports.Add(t);
        }
        ports.Sort(delegate (Transform a, Transform b) { return a.position.y.CompareTo(b.position.y); });

        // Dan's revision: the previous white hull doors, two states, no animation.
        string artSource;
        whiteClosed = FindWhiteDoorArt(out artSource);
        string openHow;
        whiteOpen = BuildOpenAperture(whiteClosed, out openHow);
        LastDoorArtSummary = "closed art " + (whiteClosed != null ? whiteClosed.name : "none") + " + open art " +
                             (whiteOpen != null ? whiteOpen.name : "none") + " (" + openHow + ") from " + artSource +
                             " - the original white hull-door set (" + WhiteDoorArtPath + "); two states switched " +
                             "instantly, no opening/closing frames, hub interior airlock art not used";

        for (int i = 0; i < p.gapCells.Count; i++)
        {
            var world = wallMap.GetCellCenterWorld(p.gapCells[i]);
            var go = new GameObject("StationAirlock#" + i);
            go.transform.SetParent(bay.transform, true);
            go.transform.position = new Vector3(world.x, world.y, 0f);

            // Clone the paired port's own panel, so the station door is authored exactly like the shuttle's port:
            // the same white sprite and the same Z the ports carry (90 on the east hull, 270 on the west hull).
            var source = ports.Count == 0 ? null : ports[Mathf.Min(i, ports.Count - 1)].GetComponentInChildren<SpriteRenderer>(true);
            SpriteRenderer panel = null;
            if (source != null)
            {
                var clone = Instantiate(source.gameObject, go.transform);
                clone.name = "panel";
                clone.transform.localPosition = Vector3.zero;
                clone.transform.localRotation = Quaternion.Euler(0f, 0f, source.transform.localEulerAngles.z);
                clone.transform.localScale = Vector3.one;
                panel = clone.GetComponent<SpriteRenderer>();
                // Scenery, not an obstacle: the wall tile this door replaces is already gone.
                var junk = clone.GetComponentsInChildren<Collider2D>(true);
                for (int j = 0; j < junk.Length; j++) Destroy(junk[j]);
                if (panel != null && wallRenderer != null)
                {
                    panel.sortingLayerID = wallRenderer.sortingLayerID;
                    panel.sortingOrder = wallRenderer.sortingOrder + 1;
                }
            }

            var door = go.AddComponent<ProximityAirlock>();
            door.panel = panel;
            door.passageAlongLocalY = false;      // the passage runs through the wall, not up and down it
            door.logTransitions = true;
            CopyInteraction(door, reference);
            ApplyWhiteDoorStates(door, whiteClosed, whiteOpen);
            if (panel != null && whiteClosed != null) panel.sprite = whiteClosed;
            stationDoors.Add(door);
            LastDockDoorCells++;
        }
        stationDoors.Sort(delegate (ProximityAirlock a, ProximityAirlock b) { return a.transform.position.y.CompareTo(b.transform.position.y); });

        // The other half of every pair: the shuttle's own docking ports. The sprite they are authored with IS the
        // closed state, so it is kept exactly as it is - never replaced with another door's art.
        for (int i = 0; i < ports.Count; i++)
        {
            var t = ports[i];
            var door = t.GetComponent<ProximityAirlock>();
            if (door == null) door = t.gameObject.AddComponent<ProximityAirlock>();
            door.panel = t.GetComponentInChildren<SpriteRenderer>(true);
            door.passageAlongLocalY = false;
            door.logTransitions = true;
            CopyInteraction(door, reference);
            Sprite own = door.panel != null ? door.panel.sprite : null;
            ApplyWhiteDoorStates(door, own != null ? own : whiteClosed, whiteOpen);
            if (door.panel != null && door.panel.sprite == null && door.closedSprite != null) door.panel.sprite = door.closedSprite;
            dockingDoors.Add(door);
            LastDockShipDoors++;
        }
        dockingDoors.Sort(delegate (ProximityAirlock a, ProximityAirlock b) { return a.transform.position.y.CompareTo(b.transform.position.y); });

        LastDockSummary = LastDockDoorCells + " station door(s) at [" + CellsText(p.gapCells) + "] in the " + p.outwardName +
                          " wall paired with " + LastDockShipDoors + " shuttle docking port(s) on its " + facing + " hull" +
                          (stationDoors.Count > 0 && dockingDoors.Count > 0 ? "; door-to-port row deltas " + DoorRowDeltas() : "") +
                          "; " + LastDoorArtSummary + "; proximity thresholds from " +
                          (reference != null ? reference.name : "the component defaults");
        Debug.Log(LogTag + " station docking: " + LastDockSummary);
    }

    // ------------------------------------------------------------------ door art (Dan's revision)
    //
    // Dan's revision: use the PREVIOUS white hull doors again, with just two states - open and closed - and no
    // opening/closing animation. The original white set in this project is
    // Assets/Sprites/SS14/SalvageShuttle/Doors/GlassShuttle/closed.png, the art the ship's own docking ports are
    // authored with; it has no open frame of its own, so the open state is built from that same art: the door's own
    // frame ring is kept and the aperture inside it is cleared, leaving a visibly open doorway with the white frame
    // retained. Nothing is substituted and no new art is added.
    const string WhiteDoorArtPath = "Assets/Sprites/SS14/SalvageShuttle/Doors/GlassShuttle/closed.png";
    const int OpenFrameRingPixels = 3;

    // The white hull-door art, read from the shuttle's own docking ports (never from the hub's interior airlocks).
    Sprite FindWhiteDoorArt(out string source)
    {
        source = "none";
        var all = shipRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null || !t.name.StartsWith("AirlockExternal")) continue;
            var sr = t.GetComponentInChildren<SpriteRenderer>(true);
            if (sr == null || sr.sprite == null) continue;
            source = t.name + " (sprite " + sr.sprite.name + ", texture " + TextureName(sr.sprite) + ")";
            return sr.sprite;
        }
        Debug.LogError(LogTag + " marked dock: the shuttle's docking ports carry no door art to reuse");
        return null;
    }

    static string TextureName(Sprite s)
    {
        return s != null && s.texture != null ? s.texture.name : "none";
    }

    // The open state: the same art with everything inside the door's own frame ring cleared to transparent.
    Sprite BuildOpenAperture(Sprite closed, out string how)
    {
        how = "no open art built (the closed art could not be read)";
        if (closed == null || closed.texture == null) return null;
        if (cachedClosed == closed && cachedOpen != null)      // built once; every later map reuses it
        {
            how = "reused from the first build, " + OpenFrameRingPixels + " px frame ring kept";
            return cachedOpen;
        }

        string readHow;
        var tex = ReadableCopy(closed.texture, out readHow);
        if (tex == null) return null;

        int rectX = Mathf.RoundToInt(closed.rect.xMin), rectY = Mathf.RoundToInt(closed.rect.yMin);
        int w = Mathf.RoundToInt(closed.rect.width), h = Mathf.RoundToInt(closed.rect.height);
        var px = tex.GetPixels32();
        int cleared = 0;
        for (int y = OpenFrameRingPixels; y < h - OpenFrameRingPixels; y++)
        {
            for (int x = OpenFrameRingPixels; x < w - OpenFrameRingPixels; x++)
            {
                int idx = (rectY + y) * tex.width + (rectX + x);
                if (idx < 0 || idx >= px.Length || px[idx].a == 0) continue;
                var c = px[idx];
                c.a = 0;
                px[idx] = c;
                cleared++;
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false);
        var pivot = new Vector2(closed.pivot.x / Mathf.Max(1f, closed.rect.width), closed.pivot.y / Mathf.Max(1f, closed.rect.height));
        var open = Sprite.Create(tex, closed.rect, pivot, closed.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        open.name = "white_open";
        cachedClosed = closed;
        cachedOpen = open;
        how = readHow + ", " + cleared + " aperture pixel(s) cleared, " + OpenFrameRingPixels + " px frame ring kept";
        return open;
    }

    // A readable copy of the door art. The importer keeps the art non-readable, so the usual path is a one-off GPU
    // copy into a render texture: no asset, importer setting or art file is touched.
    Texture2D ReadableCopy(Texture2D src, out string how)
    {
        how = "not read";
        try
        {
            var cpu = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            cpu.SetPixels32(src.GetPixels32());
            cpu.Apply(false);
            how = "CPU copy";
            return cpu;
        }
        catch (System.Exception) { /* not CPU-readable: use the GPU copy below */ }

        try
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var gpu = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            gpu.ReadPixels(new Rect(0f, 0f, src.width, src.height), 0, 0, false);
            gpu.Apply(false);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            how = "GPU copy";
            return gpu;
        }
        catch (System.Exception e)
        {
            Debug.LogError(LogTag + " marked dock: the white door art could not be read (" + e.Message +
                           "); the doors keep the closed art only");
            how = "art unreadable: " + e.Message;
            return null;
        }
    }

    // How far each station door sits off the shuttle port it faces, along the wall: 0 means a straight line-up.
    string DoorRowDeltas()
    {
        string s = "";
        int n = Mathf.Min(stationDoors.Count, dockingDoors.Count);
        for (int i = 0; i < n; i++)
        {
            float dy = stationDoors[i].transform.position.y - dockingDoors[i].transform.position.y;
            s += (s.Length > 0 ? ", " : "") + "door" + i + "-port" + i + " " + dy.ToString("0.###");
        }
        return s;
    }

    // Interaction source: an airlock already wired in the hub, taken for its proximity thresholds only - never for
    // its art, because the station doors and the shuttle's ports use the ship's own white hull-door art instead.
    ProximityAirlock FindInteractionReference()
    {
        var doors = shipRoot.GetComponentsInChildren<ProximityAirlock>(true);
        ProximityAirlock fallback = null;
        for (int i = 0; i < doors.Length; i++)
        {
            var d = doors[i];
            if (d == null) continue;
            if (d.name.StartsWith("AirlockSalvage")) return d;
            if (fallback == null) fallback = d;
        }
        if (fallback == null)
            Debug.LogWarning(LogTag + " no wired airlock in the hub for the proximity thresholds; using the component defaults");
        return fallback;
    }

    static void CopyInteraction(ProximityAirlock door, ProximityAirlock reference)
    {
        if (door == null || reference == null) return;
        door.openDistance = reference.openDistance;
        door.holdDistance = reference.holdDistance;
        door.apertureHalfWidth = reference.apertureHalfWidth;
        door.closeDelay = reference.closeDelay;
    }

    // Two states, no animation: one frame each with a zero delay, so Opening/Closing finish inside the same tick that
    // starts them. The doorway is either shut on the white closed art or visibly open on the derived aperture.
    static void ApplyWhiteDoorStates(ProximityAirlock door, Sprite closed, Sprite open)
    {
        if (door == null) return;
        door.closedSprite = closed;
        door.openSprite = open;
        door.openingFrames = new Sprite[] { open };
        door.closingFrames = new Sprite[] { closed };
        door.openingDelays = new float[] { 0f };
        door.closingDelays = new float[] { 0f };
        door.SetStateImmediate(ProximityAirlock.DoorState.Closed);   // start shut, on the door's own white art
    }

    struct Placement
    {
        public Vector3 root;
        public Vector3Int gap;
        public List<Vector3Int> gapCells;
        public Vector3Int outward;
        public string outwardName;
        public float row;
        public int walkCells;
        public int voidWalkCells;
        public string note;
    }

    static readonly Vector3Int[] Dirs =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0)
    };

    // The strip the player (and anything they drag) has to walk: from the map opening, through the ship's hull, to
    // a little way onto the deck. Direction aware, because the ship can be staged east/west or north/south of the
    // opening it was given. Returns how many of the ship's colliders sit in it, or -1 when there is no opening.
    int CorridorBlockers(Placement p, Tilemap wallMap)
    {
        var strip = CorridorLocalStrip(p, wallMap);
        if (!strip.HasValue) return -1;
        return CollidersIn(shipRoot, strip.Value, false);
    }

    // Ship colliders inside a ship-local rect. `cargo` picks which side is counted: true = deck cargo (loose items
    // and the storage crate, which must never be switched off), false = ship structure (hull and fixtures).
    int CollidersIn(Transform root, Rect rect, bool cargo)
    {
        var cols = root.GetComponentsInChildren<Collider2D>(true);
        int n = 0;
        for (int i = 0; i < cols.Length; i++)
        {
            var c = cols[i];
            if (c == null) continue;
            bool isCargo = c.GetComponentInParent<ItemComponent>() != null || c.GetComponentInParent<StorageCrate>() != null;
            if (isCargo != cargo) continue;
            var b = ToSpace(c.bounds, root);
            if (b.max.x <= rect.xMin || b.min.x >= rect.xMax) continue;
            if (b.max.y <= rect.yMin || b.min.y >= rect.yMax) continue;
            n++;
        }
        return n;
    }

    Rect? CorridorLocalStrip(Placement p, Tilemap wallMap)
    {
        if (p.gapCells == null || p.gapCells.Count == 0) return null;
        float xmin = float.MaxValue, xmax = float.MinValue, ymin = float.MaxValue, ymax = float.MinValue;
        for (int i = 0; i < p.gapCells.Count; i++)
        {
            var c = wallMap.GetCellCenterWorld(p.gapCells[i]);
            xmin = Mathf.Min(xmin, c.x - 0.5f);
            xmax = Mathf.Max(xmax, c.x + 0.5f);
            ymin = Mathf.Min(ymin, c.y - 0.5f);
            ymax = Mathf.Max(ymax, c.y + 0.5f);
        }
        var cargo = CargoRectWorld();
        Rect world;
        if (p.outward.x > 0) world = Rect.MinMaxRect(xmin, ymin, cargo.xMin + 1.5f, ymax);
        else if (p.outward.x < 0) world = Rect.MinMaxRect(cargo.xMax - 1.5f, ymin, xmax, ymax);
        else if (p.outward.y > 0) world = Rect.MinMaxRect(xmin, ymin, xmax, cargo.yMin + 1.5f);
        else world = Rect.MinMaxRect(xmin, cargo.yMax - 1.5f, xmax, ymax);
        return new Rect(world.xMin - shipRoot.position.x, world.yMin - shipRoot.position.y, world.width, world.height);
    }

    // Last resort when the authored hull still blocks the strip: disable just those colliders so the doorway is
    // walkable. Runtime-only and reversible (no scene asset changes); every cleared collider is named in the log.
    int OpenBoardingAperture(Placement p, Tilemap wallMap)
    {
        var strip = CorridorLocalStrip(p, wallMap);
        if (!strip.HasValue) return 0;
        var s = strip.Value;
        var cols = shipRoot.GetComponentsInChildren<Collider2D>(true);
        int cleared = 0;
        string names = "";
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || !cols[i].enabled) continue;
            // Never switch off deck cargo: the crate and loose items are things the player interacts with.
            if (cols[i].GetComponentInParent<ItemComponent>() != null || cols[i].GetComponentInParent<StorageCrate>() != null) continue;
            var b = ToSpace(cols[i].bounds, shipRoot);
            if (b.max.x <= s.xMin || b.min.x >= s.xMax) continue;
            if (b.max.y <= s.yMin || b.min.y >= s.yMax) continue;
            cols[i].enabled = false;
            cleared++;
            if (names.Length < 160) names += (names.Length > 0 ? ", " : "") + cols[i].gameObject.name;
        }
        if (cleared > 0) Physics2D.SyncTransforms();
        Debug.Log(LogTag + " boarding aperture: cleared " + cleared + " hull collider(s) at the connection row" +
                  (names.Length > 0 ? " (" + names + ")" : ""));
        return cleared;
    }

    static void FindMaps(Scene scene, out Tilemap wall, out Tilemap floor)
    {
        wall = null; floor = null;
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var maps = roots[i].GetComponentsInChildren<Tilemap>(true);
            for (int j = 0; j < maps.Length; j++)
            {
                if (maps[j] == null) continue;
                if (maps[j].name == "wall" && maps[j].GetComponent<TilemapCollider2D>() != null) wall = maps[j];
                else if (maps[j].name == "floor") floor = maps[j];
            }
        }
    }

    static HashSet<Vector3Int> Cells(Tilemap map)
    {
        var set = new HashSet<Vector3Int>();
        var bounds = map.cellBounds;
        var all = bounds.allPositionsWithin;
        while (all.MoveNext()) if (map.GetTile(all.Current) != null) set.Add(all.Current);
        return set;
    }

    static Dictionary<Vector3Int, int> Walkable(HashSet<Vector3Int> floors, Vector3Int from)
    {
        var dist = new Dictionary<Vector3Int, int>();
        var queue = new Queue<Vector3Int>();
        dist[from] = 0;
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            var d = dist[c];
            for (int i = 0; i < Dirs.Length; i++)
            {
                var n = c + Dirs[i];
                if (!floors.Contains(n) || dist.ContainsKey(n)) continue;
                dist[n] = d + 1;
                queue.Enqueue(n);
            }
        }
        return dist;
    }

    // Tilemap.cellBounds is a BoundsInt, which has no Encapsulate in this Unity version, so the union of the
    // wall and floor painted areas is built by hand.
    static void UnionCells(BoundsInt a, BoundsInt b, out int xMin, out int xMax, out int yMin, out int yMax)
    {
        xMin = Mathf.Min(a.xMin, b.xMin);
        xMax = Mathf.Max(a.xMax, b.xMax);
        yMin = Mathf.Min(a.yMin, b.yMin);
        yMax = Mathf.Max(a.yMax, b.yMax);
    }

    // The void that surrounds the authored layout, found by flooding outward from a ring just outside the painted
    // cell bounds. A wall tile whose void neighbour belongs to THIS set is a real way out of the map; void sealed
    // inside the maze is not. Parking next to a sealed pocket is what put the ship on top of the authored tiles.
    static HashSet<Vector3Int> OuterVoid(Tilemap wallMap, Tilemap floorMap, HashSet<Vector3Int> walls, HashSet<Vector3Int> floors)
    {
        int xMin, xMax, yMin, yMax;
        UnionCells(wallMap.cellBounds, floorMap.cellBounds, out xMin, out xMax, out yMin, out yMax);
        var min = new Vector3Int(xMin - 1, yMin - 1, 0);
        var max = new Vector3Int(xMax + 1, yMax + 1, 0);
        var outer = new HashSet<Vector3Int>();
        var queue = new Queue<Vector3Int>();
        for (int x = min.x; x <= max.x; x++) { SeedOuter(new Vector3Int(x, min.y, 0), walls, floors, outer, queue); SeedOuter(new Vector3Int(x, max.y, 0), walls, floors, outer, queue); }
        for (int y = min.y; y <= max.y; y++) { SeedOuter(new Vector3Int(min.x, y, 0), walls, floors, outer, queue); SeedOuter(new Vector3Int(max.x, y, 0), walls, floors, outer, queue); }
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            for (int i = 0; i < Dirs.Length; i++)
            {
                var n = c + Dirs[i];
                if (n.x < min.x || n.x > max.x || n.y < min.y || n.y > max.y) continue;
                SeedOuter(n, walls, floors, outer, queue);
            }
        }
        return outer;
    }

    static void SeedOuter(Vector3Int c, HashSet<Vector3Int> walls, HashSet<Vector3Int> floors, HashSet<Vector3Int> outer, Queue<Vector3Int> queue)
    {
        if (walls.Contains(c) || floors.Contains(c) || !outer.Add(c)) return;
        queue.Enqueue(c);
    }

    bool TryFindPlacement(Tilemap wallMap, Tilemap floorMap, Vector3 arrival, out Placement placement)
    {
        placement = new Placement();
        var walls = Cells(wallMap);
        var floors = Cells(floorMap);
        if (walls.Count == 0 || floors.Count == 0) return false;

        var start = wallMap.WorldToCell(arrival);
        var snapped = SnapToFloor(start, floors, floorMap);
        if (!snapped.HasValue) return false;

        // BFS across the walkable floor from the arrival cell, so the opening we cut is the one the player can
        // actually walk to - the shortest route out of the authored layout.
        var dist = Walkable(floors, snapped.Value);

        // Only openings onto the OUTSIDE of the map count (see OuterVoid).
        var outer = OuterVoid(wallMap, floorMap, walls, floors);

        var openings = new List<Placement>();
        int rejectedSealed = 0, rejectedNoFloor = 0;
        foreach (var w in walls)
        {
            int walk = int.MaxValue;
            for (int i = 0; i < Dirs.Length; i++)
            {
                int d;
                if (floors.Contains(w + Dirs[i]) && dist.TryGetValue(w + Dirs[i], out d)) walk = Mathf.Min(walk, d + 1);
            }
            for (int i = 0; i < Dirs.Length; i++)
            {
                var outward = Dirs[i];
                if (!outer.Contains(w + outward)) { rejectedSealed++; continue; }
                if (walk == int.MaxValue) { rejectedNoFloor++; continue; }
                var o = new Placement();
                o.gap = w;
                o.outward = outward;
                o.outwardName = NameOutward(outward);
                o.walkCells = walk;
                o.gapCells = new List<Vector3Int>();
                openings.Add(o);
            }
        }
        if (openings.Count == 0)
        {
            // No guesswork: report the real geometry so the placement can be fixed from data.
            Debug.LogError(LogTag + " '" + wallMap.name + "': no outer-wall opening. floors=" + floors.Count +
                           " walls=" + walls.Count + " reachableFloorCells=" + dist.Count + " outerVoidCells=" + outer.Count +
                           " rejected(sealed void next to it)=" + rejectedSealed + " rejected(no reachable floor)=" + rejectedNoFloor +
                           "; arrivalCell=" + snapped.Value.ToString() +
                           (floors.Contains(snapped.Value) ? " (arrival IS on a floor tile)" : " (arrival is NOT on a floor tile)"));
            return false;
        }

        // Nearest opening the player can reach first; then the one nearest the arrival row/column.
        openings.Sort(delegate (Placement a, Placement b)
        {
            int cmp = a.walkCells.CompareTo(b.walkCells);
            if (cmp != 0) return cmp;
            var pa = wallMap.GetCellCenterWorld(a.gap);
            var pb = wallMap.GetCellCenterWorld(b.gap);
            cmp = Mathf.Abs(pa.y - arrival.y).CompareTo(Mathf.Abs(pb.y - arrival.y));
            if (cmp != 0) return cmp;
            return Mathf.Abs(pa.x - arrival.x).CompareTo(Mathf.Abs(pb.x - arrival.x));
        });

        int tried = 0;
        for (int i = 0; i < openings.Count && i < 96; i++)
        {
            // Smallest clearance first: the ship should sit just outside the wall, not far out in the void. If the
            // map is still deeper than that in this direction, step further out until the footprint is clear.
            for (float clearance = 2.5f; clearance <= 40f; clearance += 2f)
            {
                var p = BuildPlacement(wallMap, walls, openings[i].gap, openings[i].outward, openings[i].walkCells, clearance);
                if (p.gapCells.Count == 0) continue;
                tried++;
                if (CountAuthoredCellsInside(wallMap, walls, floors, p.root) > 0) continue;
                placement = p;
                return true;
            }
        }
        Debug.LogError(LogTag + " '" + wallMap.name + "': openings=" + openings.Count + " placementsTried=" + tried +
                       " - no opening produced a clear staging spot");
        return false;
    }

    static string NameOutward(Vector3Int outward)
    {
        if (outward.x > 0) return "east";
        if (outward.x < 0) return "west";
        if (outward.y > 0) return "north";
        return "south";
    }

    Placement BuildPlacement(Tilemap wallMap, HashSet<Vector3Int> walls, Vector3Int gap, Vector3Int outward, int walk, float clearance)
    {
        var p = new Placement();
        p.gap = gap;
        p.outward = outward;
        p.outwardName = NameOutward(outward);
        p.walkCells = walk;
        p.voidWalkCells = Mathf.Max(0, Mathf.RoundToInt(clearance - 1.5f));
        var gapCentre = wallMap.GetCellCenterWorld(gap);

        if (outward.x != 0)
        {
            // Doorway rows: the ship's hull lines are mostly open (SS14 shuttle hulls are sparse), so park with the
            // nearest free hull row level with the opening.
            bool shipEast = outward.x > 0;
            p.row = ConnectionRow(shipEast);
            float hullFace = shipEast ? hullWestX : hullEastX;
            p.root = new Vector3(gapCentre.x + clearance * outward.x - hullFace, gapCentre.y - p.row, 0f);
            // A doorway is three tiles tall: the wall tile itself plus one either side of it.
            p.gapCells = new List<Vector3Int>();
            for (int dy = -1; dy <= 1; dy++)
            {
                var c = new Vector3Int(gap.x, gap.y + dy, 0);
                if (walls.Contains(c)) p.gapCells.Add(c);
            }
        }
        else
        {
            // Same, along the other axis: a north/south exit needs a free hull column level with the opening.
            bool shipNorth = outward.y > 0;
            p.row = ConnectionColumn(shipNorth);
            float hullFace = shipNorth ? hullSouthY : hullNorthY;
            p.root = new Vector3(gapCentre.x - p.row, gapCentre.y + clearance * outward.y - hullFace, 0f);
            p.gapCells = new List<Vector3Int>();
            for (int dx = -1; dx <= 1; dx++)
            {
                var c = new Vector3Int(gap.x + dx, gap.y, 0);
                if (walls.Contains(c)) p.gapCells.Add(c);
            }
        }
        p.note = "staged " + p.outwardName + " of the opening, " + clearance.ToString("0.#") + " tile(s) clear of the map wall";
        return p;
    }

    float ConnectionRow(bool westSide)
    {
        // Prefer the ship's own airlock row when it sits on the half of the ship facing the map; otherwise use the
        // nearest row of the facing hull that is not solid.
        if (westSide && hasWestDoor) return doorWestRow;
        if (!westSide && hasEastDoor) return doorEastRow;
        return NearestFreeRow(westSide ? hullWestX : hullEastX, westSide);
    }

    float NearestFreeRow(float hullX, bool westSide)
    {
        float best = shipLocalBounds.center.y;
        float bestScore = float.MaxValue;
        float lo = shipLocalBounds.min.y + 0.5f;
        float hi = shipLocalBounds.max.y - 0.5f;
        for (float y = Mathf.Floor(lo) + 0.5f; y < hi; y += 1f)
        {
            bool blocked = false;
            for (int i = 0; i < hullColliders.Count; i++)
            {
                var b = hullColliders[i];
                var inflated = new Bounds(b.center, b.size + new Vector3(0.02f, 0.02f, 0f));
                if (Mathf.Abs(b.center.x - hullX) <= 0.75f && inflated.Contains(new Vector3(hullX, y, 0f))) { blocked = true; break; }
            }
            if (blocked) continue;
            float score = Mathf.Abs(y - shipLocalBounds.center.y);
            if (score < bestScore) { bestScore = score; best = y; }
        }
        return best;
    }

    // The same free-line search down the hull, for a ship parked north or south of its opening.
    float ConnectionColumn(bool northSide)
    {
        float hullY = northSide ? hullSouthY : hullNorthY;
        float best = cargoRect.center.x;
        float bestScore = float.MaxValue;
        float lo = shipLocalBounds.min.x + 0.5f;
        float hi = shipLocalBounds.max.x - 0.5f;
        for (float x = Mathf.Floor(lo) + 0.5f; x < hi; x += 1f)
        {
            bool blocked = false;
            for (int i = 0; i < hullColliders.Count; i++)
            {
                var b = hullColliders[i];
                var inflated = new Bounds(b.center, b.size + new Vector3(0.02f, 0.02f, 0f));
                if (Mathf.Abs(b.center.y - hullY) <= 0.75f && inflated.Contains(new Vector3(x, hullY, 0f))) { blocked = true; break; }
            }
            if (blocked) continue;
            float score = Mathf.Abs(x - cargoRect.center.x);
            if (score < bestScore) { bestScore = score; best = x; }
        }
        return best;
    }

    // A spot that is provably clear of every authored tile: the ship's whole footprint placed beyond the map's own
    // tile bounds, on the side that needs the smallest move, with a real outer-wall tile opened onto it. Only ever
    // used if the normal search above finds nothing - it never overlaps the authored map.
    Placement OutsidePlacement(Tilemap wallMap, Tilemap floorMap, Vector3 arrival)
    {
        var walls = Cells(wallMap);
        var floors = Cells(floorMap);
        int xMin, xMax, yMin, yMax;
        UnionCells(wallMap.cellBounds, floorMap.cellBounds, out xMin, out xMax, out yMin, out yMax);
        var cell = wallMap.cellSize;
        var centre = new Vector3(
            (wallMap.GetCellCenterWorld(new Vector3Int(xMin, yMin, 0)).x + wallMap.GetCellCenterWorld(new Vector3Int(xMax - 1, yMin, 0)).x) * 0.5f,
            (wallMap.GetCellCenterWorld(new Vector3Int(xMin, yMin, 0)).y + wallMap.GetCellCenterWorld(new Vector3Int(xMin, yMax - 1, 0)).y) * 0.5f, 0f);

        Placement best = new Placement();
        float bestMove = float.MaxValue;
        bool found = false;
        for (int s = 0; s < Dirs.Length; s++)
        {
            var outward = Dirs[s];
            var gap = NearestOuterWall(wallMap, walls, centre, outward);
            if (!gap.HasValue) continue;
            for (float clearance = 2.5f; clearance <= 80f; clearance += 2f)
            {
                var p = BuildPlacement(wallMap, walls, gap.Value, outward, -1, clearance);
                if (p.gapCells.Count == 0) continue;
                if (CountAuthoredCellsInside(wallMap, walls, floors, p.root) > 0) continue;
                float move = outward.x != 0 ? Mathf.Abs(p.root.x - arrival.x) : Mathf.Abs(p.root.y - arrival.y);
                if (move < bestMove) { bestMove = move; best = p; found = true; }
                break;
            }
        }
        if (found)
        {
            best.note = "last resort: parked outside the map's own tile bounds (" + best.outwardName + "), never over authored tiles";
            return best;
        }

        // Nothing cleared at all: park east of the arrival and say exactly that, so it can never be mistaken for a
        // good placement. Never reached in practice.
        var fallback = new Placement();
        fallback.root = new Vector3(arrival.x + 6.5f, arrival.y - cargoRect.center.y, 0f);
        fallback.gap = wallMap.WorldToCell(arrival);
        fallback.gapCells = new List<Vector3Int>();
        fallback.outward = new Vector3Int(1, 0, 0);
        fallback.outwardName = "east";
        fallback.walkCells = -1;
        fallback.voidWalkCells = -1;
        fallback.row = cargoRect.center.y;
        fallback.note = "NO outer-wall opening at all: parked east of the arrival (overlaps may remain)";
        return fallback;
    }

    // The outer-wall tile on the requested side of the map whose row/column is nearest the arrival.
    Vector3Int? NearestOuterWall(Tilemap wallMap, HashSet<Vector3Int> walls, Vector3 centre, Vector3Int outward)
    {
        Vector3Int? bestCell = null;
        float best = float.MaxValue;
        foreach (var w in walls)
        {
            var p = wallMap.GetCellCenterWorld(w);
            if (outward.x > 0 && p.x < centre.x) continue;
            if (outward.x < 0 && p.x > centre.x) continue;
            if (outward.y > 0 && p.y < centre.y) continue;
            if (outward.y < 0 && p.y > centre.y) continue;
            float d = outward.x != 0 ? Mathf.Abs(p.y - centre.y) * 1000f + Mathf.Abs(p.x - centre.x)
                                     : Mathf.Abs(p.x - centre.x) * 1000f + Mathf.Abs(p.y - centre.y);
            if (d < best) { best = d; bestCell = w; }
        }
        return bestCell;
    }

    bool FootprintOverlaps(Tilemap map, HashSet<Vector3Int> walls, HashSet<Vector3Int> floors, Vector3 root)
    {
        return CountAuthoredCellsInside(map, walls, floors, root) > 0;
    }

    int CountAuthoredCellsInside(Tilemap map, HashSet<Vector3Int> walls, HashSet<Vector3Int> floors)
    {
        return CountAuthoredCellsInside(map, walls, floors, shipRoot.position);
    }

    int CountAuthoredCellsInside(Tilemap map, HashSet<Vector3Int> walls, HashSet<Vector3Int> floors, Vector3 root)
    {
        var min = root + new Vector3(shipLocalBounds.min.x, shipLocalBounds.min.y, 0f);
        var max = root + new Vector3(shipLocalBounds.max.x, shipLocalBounds.max.y, 0f);
        var lo = map.WorldToCell(min) - new Vector3Int(1, 1, 0);
        var hi = map.WorldToCell(max) + new Vector3Int(1, 1, 0);
        int count = 0;
        var size = map.cellSize;
        for (int x = lo.x; x <= hi.x; x++)
        {
            for (int y = lo.y; y <= hi.y; y++)
            {
                var c = new Vector3Int(x, y, 0);
                if (!walls.Contains(c) && !floors.Contains(c)) continue;
                var centre = map.GetCellCenterWorld(c);
                if (centre.x + size.x * 0.5f > min.x && centre.x - size.x * 0.5f < max.x &&
                    centre.y + size.y * 0.5f > min.y && centre.y - size.y * 0.5f < max.y) count++;
            }
        }
        return count;
    }

    static Vector3Int? SnapToFloor(Vector3Int cell, HashSet<Vector3Int> floors, Tilemap map)
    {
        if (floors.Contains(cell)) return cell;
        for (int r = 1; r <= 4; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    var c = new Vector3Int(cell.x + dx, cell.y + dy, 0);
                    if (floors.Contains(c)) return c;
                }
            }
        }
        return null;
    }

    static string CellsText(List<Vector3Int> cells)
    {
        var s = "";
        for (int i = 0; i < cells.Count; i++)
        {
            if (i > 0) s += " ";
            s += cells[i].x + "," + cells[i].y;
        }
        return s;
    }

    Rect CargoRectWorld()
    {
        var min = shipRoot.TransformPoint(new Vector3(cargoRect.xMin, cargoRect.yMin, 0f));
        var max = shipRoot.TransformPoint(new Vector3(cargoRect.xMax, cargoRect.yMax, 0f));
        return new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
    }

    // ------------------------------------------------------------------ cargo: what is physically on the deck
    void Update()
    {
        if (!HasPersistentShip || Time.timeScale == 0f) return;
        if (Time.unscaledTime < nextCargoScan) return;
        nextCargoScan = Time.unscaledTime + 0.25f;
        ScanCargo();
    }

    void ScanCargo()
    {
        var dragger = Object.FindFirstObjectByType<ItemDragSystem>();
        var items = Object.FindObjectsByType<ItemComponent>(FindObjectsSortMode.None);
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (item == null || item.Container != null || !item.gameObject.activeInHierarchy) continue;
            if (dragger != null && dragger.DraggedItem == item) continue;   // a live drag owns its own item
            var t = item.transform;
            bool aboard = t.IsChildOf(shipRoot);
            bool inside = cargoRect.Contains(shipRoot.InverseTransformPoint(t.position));
            if (inside == aboard) continue;
            if (inside)
            {
                t.SetParent(cargoAnchor, true);
                Debug.Log(LogTag + " aboard: " + item.name + " (" + (item.Definition != null ? item.Definition.name : "?") +
                          ") at ship-local " + shipRoot.InverseTransformPoint(t.position).ToString("0.##"));
            }
            else
            {
                // Dragged off the deck: it belongs to the map again and must not travel with the ship.
                t.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(item.gameObject, SceneManager.GetActiveScene());
                Debug.Log(LogTag + " ashore: " + item.name + " left the cargo volume and stays with the map");
            }
        }
    }

    public int AboardCount()
    {
        if (!HasPersistentShip) return 0;
        var items = Object.FindObjectsByType<ItemComponent>(FindObjectsSortMode.None);
        int n = 0;
        for (int i = 0; i < items.Length; i++) if (items[i] != null && items[i].transform.IsChildOf(shipRoot)) n++;
        return n;
    }

    // ------------------------------------------------------------------ helpers used by the travel plumbing
    /// <summary>True when this transform belongs to the persistent ship (deck cargo or the ship's own fixtures).</summary>
    public static bool IsAboardShip(Transform t)
    {
        return Instance != null && Instance.HasPersistentShip && t != null && t.IsChildOf(Instance.shipRoot);
    }

    /// <summary>
    /// The container a scene hop may snapshot as the player's "stowed" items. The ship's own crate travels with
    /// the ship, so it is never that container: capturing it as well would duplicate its contents into whatever
    /// container the destination offers.
    /// </summary>
    public static InventoryContainer SceneStorageContainer()
    {
        if (Instance == null || !Instance.HasPersistentShip)
            return Object.FindFirstObjectByType<InventoryContainer>();
        var containers = Object.FindObjectsByType<InventoryContainer>(FindObjectsSortMode.None);
        for (int i = 0; i < containers.Length; i++)
        {
            var c = containers[i];
            if (c == null || !c.isActiveAndEnabled) continue;
            if (IsAboardShip(c.transform)) continue;
            return c;
        }
        return null;
    }
}
