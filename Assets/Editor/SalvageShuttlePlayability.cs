using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Makes the baked salvageshuttle *playable*: collision derived from the same layout data the
/// bake reads, plus the console scene-transition moved off the old placeholder console.
///
/// Everything here is derived from Assets/ShipData/salvageshuttle.layout.tsv - no hand-placed
/// colliders. The rule is a whitelist of walkable art: only floor / catwalk / grille tiles stay
/// open, every other tile in the map is structure or equipment and blocks movement:
///
///   * half-tile triangles for the two diagonal wall / window families (the art is a half-tile
///     triangle - measured, see DiagonalBase),
///   * full-tile boxes for walls, windows, secret doors, hull-side airlocks and equipment,
///   * a box on each interior airlock, parked on ProximityAirlock.closedCollider so the door
///     blocks while it is shut and releases the moment it opens,
///   * a box plus a ScenePortal on the ship console (ComputerShuttle#2).
///
/// Seat furniture (Chair*, ChairPilotSeat) is deliberately left walkable: the Player spawns at
/// (4.14, -0.09), which is inside Chair#125's tile (4.5, -0.5), so blocking chairs would start the
/// scene with the player inside a collider. Reported, not silently blocked.
///
/// Both steps are idempotent: Wire() strips every collider/ScenePortal it owns before rebuilding,
/// so it can be re-run after any bake.
/// </summary>
public static class SalvageShuttlePlayability
{
    const string ScenePath = "Assets/Scenes/HomeShip.unity";
    const string LayoutPath = "Assets/ShipData/salvageshuttle.layout.tsv";
    const string ShipRoot = "SalvageShuttle";

    /// <summary>The console that should take over the scene transition (top wall of the cockpit).</summary>
    const string ConsoleObject = "ComputerShuttle#2";

    /// <summary>The leftover placeholder console to remove.</summary>
    const string OldConsoleObject = "ShuttleConsole";

    /// <summary>Fallback destination on the console portal (used only when no roll is configured); DebugArena portals back to HomeShip.</summary>
    const string ConsoleDestination = "DebugArena";

    /// <summary>
    /// Dan's ask: the shuttle console rolls between these two arenas on EVERY launch, so the portal
    /// carries them as a list and picks one per trip (UnityEngine.Random.Range). DebugArena stays the
    /// fallback destinationScene and stays enabled in Build Settings, so nothing loses its entry point.
    /// </summary>
    static readonly string[] ConsoleRandomDestinations = { "LargeBattleArena", "LargeExploreArena" };

    const string ScratchDir = "/home/dan/.hermes/profiles/coder/cache/scratch";
    const string ReportPath = ScratchDir + "/playability-report.txt";

    // ---- classification -------------------------------------------------------------------

    /// <summary>Art that is walked on. Everything else in the layout blocks movement.</summary>
    static readonly string[] WalkableFamilies = { "floor", "Catwalk", "Grille" };

    /// <summary>Seat furniture: decorative here, and one chair is under the player spawn.</summary>
    static readonly string[] FurnitureFamilies = { "Chair", "ChairPilotSeat" };

    static readonly string[] DiagonalFamilies = { "WallReinforcedDiagonal", "ReinforcedWindowDiagonal" };

    static readonly string[] InteriorDoorFamilies = { "AirlockSalvage", "AirlockEngineering" };

    /// <summary>
    /// The diagonal art is a half-tile triangle. Measured from the RSI cells themselves
    /// (Assets/Sprites/SS14/SalvageShuttle/{Walls,Windows}/*Diagonal/*.png, 32x32, 52% opaque,
    /// quadrants TL=0 TR=136 BL=136 BR=256): with the connection rotation the map applies, the
    /// fully opaque quadrant is rot 0 -> bottom-right, 90 -> top-right, 180 -> top-left,
    /// 270 -> bottom-left. This is that bottom-right half, rotated per tile.
    /// </summary>
    static readonly Vector2[] DiagonalBase =
    {
        new Vector2(-0.5f, -0.5f),
        new Vector2(0.5f, -0.5f),
        new Vector2(0.5f, 0.5f),
    };

    struct LayoutTile
    {
        public string Label;
        public string Family;
        public float X;
        public float Y;
        public float Rot;
    }

    static List<LayoutTile> ReadLayout()
    {
        var byLabel = new Dictionary<string, LayoutTile>();
        foreach (var line in File.ReadAllLines(LayoutPath))
        {
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var f = line.Split('\t');
            if (f.Length < 7) continue;
            var label = f[0];
            if (byLabel.ContainsKey(label)) continue;
            byLabel[label] = new LayoutTile
            {
                Label = label,
                Family = label.Split('#')[0],
                X = float.Parse(f[3], CultureInfo.InvariantCulture),
                Y = float.Parse(f[4], CultureInfo.InvariantCulture),
                Rot = float.Parse(f[5], CultureInfo.InvariantCulture),
            };
        }

        return byLabel.Values.OrderBy(t => t.Y).ThenBy(t => t.X).ToList();
    }

    static bool IsWalkable(string family) =>
        WalkableFamilies.Any(family.StartsWith) || FurnitureFamilies.Contains(family);

    static bool IsDiagonal(string family) => DiagonalFamilies.Contains(family);

    static bool IsInteriorDoor(string family) => InteriorDoorFamilies.Contains(family);

    static bool IsPlainWall(string family) =>
        family == "WallReinforced" || family == "ReinforcedWindow";

    /// <summary>Solid half-tile polygon for a diagonal tile, rotated by its layout rotation.</summary>
    static Vector2[] DiagonalPolygon(float rot)
    {
        float a = rot * Mathf.Deg2Rad;
        float c = Mathf.Cos(a), s = Mathf.Sin(a);
        return DiagonalBase.Select(p => new Vector2(p.x * c - p.y * s, p.x * s + p.y * c)).ToArray();
    }

    /// <summary>Centre of the fully opaque quadrant of a rotated diagonal tile.</summary>
    static Vector2 DiagonalSolidQuadrantCentre(float rot)
    {
        switch (Mathf.RoundToInt(Mathf.Repeat(rot, 360f) / 90f) % 4)
        {
            case 0: return new Vector2(0.25f, -0.25f);   // bottom-right
            case 1: return new Vector2(0.25f, 0.25f);    // top-right
            case 2: return new Vector2(-0.25f, 0.25f);   // top-left
            default: return new Vector2(-0.25f, -0.25f); // bottom-left
        }
    }

    static string Key(Vector2 v) => v.x.ToString("F1", CultureInfo.InvariantCulture) + "," +
                                   v.y.ToString("F1", CultureInfo.InvariantCulture);

    static string Key(float x, float y) => Key(new Vector2(x, y));

    // ---- wiring ---------------------------------------------------------------------------

    [MenuItem("Tools/Salvage/6 wire collision + console")]
    public static void Wire()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var tiles = ReadLayout();

        // 0) The interior doors must already carry their ProximityAirlock (step 4 of the salvage
        //    pipeline). Resolve them up front - before any collider is created - so that nothing
        //    created here can be invalidated by a scene reload.
        var missingDoors = new List<string>();
        foreach (var t in tiles.Where(x => IsInteriorDoor(x.Family)))
        {
            var doorGo = GameObject.Find(t.Label);
            var probe = doorGo != null ? doorGo.GetComponent<ProximityAirlock>() : null;
            Debug.Log($"[Play] door probe {t.Label}: gameObject={(doorGo != null ? doorGo.transform.position.ToString() : "NOT FOUND")} " +
                      $"proximityAirlock={(probe != null ? "ok" : "NULL")} " +
                      $"components={(doorGo != null ? doorGo.GetComponents<Component>().Length.ToString() : "0")}");
            if (probe == null) missingDoors.Add(t.Label);
        }

        if (missingDoors.Count > 0)
        {
            Debug.LogWarning($"[Play] doors without a ProximityAirlock: {string.Join(",", missingDoors)} - running step 4 first");
            ProximityAirlockSetup.WireInteriorAirlocks();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            tiles = ReadLayout();
        }

        var scene = EditorSceneManager.GetActiveScene();
        var ship = GameObject.Find(ShipRoot);
        if (ship == null) throw new Exception("[Play] no " + ShipRoot + " in " + ScenePath);

        int stripped = 0;
        foreach (var c in ship.GetComponentsInChildren<Collider2D>(true)) { UnityEngine.Object.DestroyImmediate(c); stripped++; }
        foreach (var p in ship.GetComponentsInChildren<ScenePortal>(true)) { UnityEngine.Object.DestroyImmediate(p); stripped++; }

        int boxes = 0, polys = 0, doors = 0, walkable = 0;
        var problems = new List<string>();

        foreach (var t in tiles)
        {
            var go = GameObject.Find(t.Label);
            if (go == null) { problems.Add("missing GameObject " + t.Label); continue; }

            var drift = Vector2.Distance(go.transform.position, new Vector2(t.X, t.Y));
            if (drift > 0.001f)
                problems.Add($"{t.Label} sits at {go.transform.position} but the layout says ({t.X},{t.Y})");

            if (IsWalkable(t.Family)) { walkable++; continue; }

            if (IsDiagonal(t.Family))
            {
                var poly = go.AddComponent<PolygonCollider2D>();
                poly.points = DiagonalPolygon(t.Rot);
                polys++;
                continue;
            }

            var box = go.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            box.offset = Vector2.zero;
            boxes++;

            if (!IsInteriorDoor(t.Family)) continue;

            var door = go.GetComponent<ProximityAirlock>();
            if (door == null)
            {
                problems.Add(t.Label + " has no ProximityAirlock to attach its blocking collider to");
                continue;
            }

            door.closedCollider = box;
            box.enabled = true;   // the door starts shut, so it starts blocking
            EditorUtility.SetDirty(door);
            doors++;
        }

        // ---- console -----------------------------------------------------------------------
        var console = GameObject.Find(ConsoleObject);
        if (console == null)
        {
            problems.Add("console " + ConsoleObject + " not found");
        }
        else
        {
            if (console.GetComponent<Collider2D>() == null)
            {
                var cc = console.AddComponent<BoxCollider2D>();
                cc.size = Vector2.one;
            }

            var player = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None).FirstOrDefault();
            if (player == null) problems.Add("no PlayerMovement in the scene to point the console portal at");

            var portal = console.AddComponent<ScenePortal>();
            var so = new SerializedObject(portal);
            so.FindProperty("destinationScene").stringValue = ConsoleDestination;
            var roll = so.FindProperty("randomDestinations");
            if (roll == null)
                problems.Add("ScenePortal.randomDestinations is not serialized - recompile the script in the editor first");
            else
            {
                roll.arraySize = ConsoleRandomDestinations.Length;
                for (int i = 0; i < ConsoleRandomDestinations.Length; i++)
                    roll.GetArrayElementAtIndex(i).stringValue = ConsoleRandomDestinations[i];
            }
            so.FindProperty("enterToTravel").boolValue = false;
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("interactionDistance").floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[Play] console portal on {ConsoleObject} at {console.transform.position} -> " +
                      $"roll({string.Join(" | ", ConsoleRandomDestinations)}) fallback {ConsoleDestination} " +
                      $"(player={(player != null ? player.gameObject.name : "null")})");
        }

        var old = GameObject.Find(OldConsoleObject);
        if (old != null)
        {
            Debug.Log($"[Play] removing old console {OldConsoleObject} at {old.transform.position}, " +
                      $"objects: {string.Join(",", old.GetComponentsInChildren<Transform>(true).Select(c => c.name))}");
            UnityEngine.Object.DestroyImmediate(old);
        }
        else
        {
            Debug.Log($"[Play] old console {OldConsoleObject} already absent");
        }

        var portals = UnityEngine.Object.FindObjectsByType<ScenePortal>(FindObjectsSortMode.None);
        if (portals.Length != 1)
            problems.Add($"expected exactly 1 ScenePortal in the scene, found {portals.Length} " +
                         $"({string.Join(",", portals.Select(p => p.gameObject.name))})");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();

        Debug.Log($"[Play] wire complete: stripped={stripped} walkableTiles={walkable} boxes={boxes} " +
                  $"diagonalPolygons={polys} doorColliders={doors} portals={portals.Length} problems={problems.Count}");
        foreach (var p in problems) Debug.LogError("[Play] PROBLEM " + p);
    }

    [MenuItem("Tools/Salvage/7 verify collision + console")]
    public static void Verify()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var ship = GameObject.Find(ShipRoot);
        if (ship == null) throw new Exception("[Play] no " + ShipRoot + " in " + ScenePath);

        var log = new List<string>();
        var failures = new List<string>();
        void Line(string s) => log.Add(s);
        void Check(bool ok, string what)
        {
            if (ok) log.Add("PASS  " + what);
            else { failures.Add(what); log.Add("FAIL  " + what); }
        }

        var tiles = ReadLayout();
        var shipSolid = ship.GetComponentsInChildren<Collider2D>(true).Where(c => c.enabled).ToArray();

        // A tile can carry several labels (floor art + wall + machine). Solidity is a property of
        // the tile, so record which tiles carry at least one blocking label.
        var blockingCells = new HashSet<string>(tiles.Where(t => !IsWalkable(t.Family)).Select(t => Key(t.X, t.Y)));

        Physics2D.SyncTransforms();

        int solidChecked = 0, walkChecked = 0, diagChecked = 0, physAgree = 0, physDisagree = 0;
        var physicsMismatches = new List<string>();

        Line("== per-tile collider coverage, derived from " + LayoutPath + " ==");
        foreach (var t in tiles)
        {
            var go = GameObject.Find(t.Label);
            if (go == null) { failures.Add("missing GameObject " + t.Label); continue; }
            var centre = new Vector2(t.X, t.Y);
            var own = go.GetComponents<Collider2D>().Where(c => c.enabled).ToArray();
            bool blockedGeometric = shipSolid.Any(c => c.OverlapPoint(centre));
            bool blockedPhysics = Physics2D.OverlapPointAll(centre)
                .Any(c => c.transform == ship.transform || c.transform.IsChildOf(ship.transform));

            if (blockedGeometric == blockedPhysics) physAgree++;
            else
            {
                physDisagree++;
                if (physicsMismatches.Count < 8)
                    physicsMismatches.Add($"{t.Label}({t.X},{t.Y}) geometric={blockedGeometric} physics={blockedPhysics}");
            }

            if (IsWalkable(t.Family))
            {
                walkChecked++;
                Check(own.Length == 0, $"{t.Label} walkable label carries no collider of its own");
                if (blockingCells.Contains(Key(t.X, t.Y)))
                    Line($"note  {t.Label} ({t.X},{t.Y}) is floor/grille art under a wall, door or machine - tile stays blocked");
                else
                    Check(!blockedGeometric, $"{t.Label} ({t.X},{t.Y}) floor tile is clear at its centre");
            }
            else if (IsDiagonal(t.Family))
            {
                diagChecked++;
                var poly = go.GetComponent<PolygonCollider2D>();
                Check(poly != null, $"{t.Label} diagonal favours a PolygonCollider2D");
                if (poly == null) continue;

                var solidQ = centre + DiagonalSolidQuadrantCentre(t.Rot);
                var emptyQ = centre - DiagonalSolidQuadrantCentre(t.Rot);
                Check(poly.OverlapPoint(solidQ), $"{t.Label} rot {t.Rot}: solid half at {solidQ} blocks");
                Check(!poly.OverlapPoint(emptyQ), $"{t.Label} rot {t.Rot}: open half at {emptyQ} is passable");
                Check(Mathf.Abs(PolygonArea(poly.points) - 0.5f) < 0.02f,
                    $"{t.Label} diagonal polygon covers half a tile ({PolygonArea(poly.points):F3})");
            }
            else
            {
                solidChecked++;
                Check(blockedGeometric, $"{t.Label} ({t.X},{t.Y}) solid tile blocks movement at its centre");
                Check(own.Length > 0, $"{t.Label} solid tile carries its own collider");
            }
        }

        Line($"tiles={tiles.Count} solidChecked={solidChecked} walkableChecked={walkChecked} diagonalsChecked={diagChecked}");
        Line($"Physics2D.OverlapPoint vs Collider2D.OverlapPoint: {physAgree} agree, {physDisagree} differ");
        foreach (var m in physicsMismatches) Line("  physics mismatch: " + m);

        // ---- interior doors -----------------------------------------------------------------
        Line("");
        Line("== interior doors block while shut, release when open ==");
        foreach (var fam in InteriorDoorFamilies)
        {
            var t = tiles.FirstOrDefault(x => x.Family == fam);
            if (t.Label == null) { failures.Add("no layout tile for door family " + fam); continue; }
            var go = GameObject.Find(t.Label);
            var door = go != null ? go.GetComponent<ProximityAirlock>() : null;
            Check(door != null, $"{t.Label} still has its ProximityAirlock");
            if (door == null) continue;

            Check(door.closedCollider != null, $"{t.Label} blocking collider is assigned to the airlock");
            door.SetStateImmediate(ProximityAirlock.DoorState.Closed);
            Check(door.closedCollider != null && door.closedCollider.enabled,
                $"{t.Label} blocks while Closed (collider enabled)");
            door.SetStateImmediate(ProximityAirlock.DoorState.Open);
            Check(door.closedCollider == null || !door.closedCollider.enabled,
                $"{t.Label} releases while Open (collider disabled)");
            door.SetStateImmediate(ProximityAirlock.DoorState.Closed);
            Line($"door {t.Label} at ({t.X},{t.Y}) re-parked Closed");
        }

        // ---- console ------------------------------------------------------------------------
        Line("");
        Line("== console scene transition ==");
        Check(GameObject.Find(OldConsoleObject) == null, $"old console {OldConsoleObject} is gone from the scene");
        var portals = UnityEngine.Object.FindObjectsByType<ScenePortal>(FindObjectsSortMode.None);
        Check(portals.Length == 1, $"exactly one ScenePortal in the scene (found {portals.Length})");
        var console = GameObject.Find(ConsoleObject);
        Check(console != null, $"new console {ConsoleObject} exists");
        var portalOnConsole = console != null ? console.GetComponent<ScenePortal>() : null;
        Check(portalOnConsole != null, $"new console {ConsoleObject} carries the ScenePortal");
        if (portalOnConsole != null)
        {
            var so = new SerializedObject(portalOnConsole);
            var dest = so.FindProperty("destinationScene").stringValue;
            var rollProp = so.FindProperty("randomDestinations");
            var roll = Enumerable.Range(0, rollProp.arraySize)
                .Select(i => rollProp.GetArrayElementAtIndex(i).stringValue).ToArray();
            var enter = so.FindProperty("enterToTravel").boolValue;
            var target = so.FindProperty("player").objectReferenceValue as PlayerMovement;
            var dist = so.FindProperty("interactionDistance").floatValue;
            Check(dest == ConsoleDestination, $"console portal fallback destination is {dest}");
            Check(!enter, "console portal is click-to-travel, not walk-into");
            Check(target != null, $"console portal has the player assigned ({(target != null ? target.gameObject.name : "null")})");
            Check(dist > 0f && dist <= 2f, $"console portal interaction distance is {dist}");
            Check(roll.Length == ConsoleRandomDestinations.Length && roll.All(ConsoleRandomDestinations.Contains),
                $"console portal rolls between {roll.Length} arena(s): {string.Join(" | ", roll)}");
            foreach (var name in ConsoleRandomDestinations)
            {
                bool inBuild = EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/" + name + ".unity"));
                Check(inBuild, $"random destination {name} is an enabled scene in Build Settings");
                Check(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/" + name + ".unity") != null,
                    $"random destination scene asset Assets/Scenes/{name}.unity exists");
                Line($"note  Application.CanStreamedLevelBeLoaded({name}) = {Application.CanStreamedLevelBeLoaded(name)} " +
                     "(edit-mode probe; Build Settings is the authority in the editor)");
            }

            // The roll must be a fresh pick per trip, not one choice stored at startup: call the same
            // method Travel() calls and expect both arenas to come back.
            var picks = Enumerable.Range(0, 200).Select(_ => portalOnConsole.PickDestination())
                .Distinct().OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Check(picks.Length == ConsoleRandomDestinations.Length,
                $"PickDestination() rolls every arena over 200 calls ({string.Join(" | ", picks)})");
            bool fallbackInBuild = EditorBuildSettings.scenes.Any(s => s.enabled && s.path.EndsWith("/" + dest + ".unity"));
            Check(fallbackInBuild, $"fallback destination {dest} is an enabled scene in Build Settings");
            Line($"note  Application.CanStreamedLevelBeLoaded({dest}) = {Application.CanStreamedLevelBeLoaded(dest)} " +
                 "(edit-mode probe; Build Settings is the authority in the editor)");
            Check(console.GetComponent<Collider2D>() != null, "console has a collider for the click test");
        }

        // ---- physics wiring -----------------------------------------------------------------
        Line("");
        Line("== physics wiring: why the colliders actually stop the player ==");
        var spawn = UnityEngine.Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None).FirstOrDefault();
        Check(spawn != null, "the scene has a PlayerMovement");
        var body = spawn != null ? spawn.GetComponent<Rigidbody2D>() : null;
        Check(body != null, "the player has a Rigidbody2D");
        if (body != null)
        {
            Check(body.bodyType == RigidbodyType2D.Dynamic, $"player rigidbody is Dynamic ({body.bodyType})");
            Check(body.simulated, $"player rigidbody is simulated ({body.simulated})");
            Line($"note  player rigidbody: type={body.bodyType} simulated={body.simulated} gravityScale={body.gravityScale} " +
                 $"collisionDetection={body.collisionDetectionMode} interpolation={body.interpolation} layer={spawn.gameObject.layer}");
        }

        var playerCollider = spawn != null ? spawn.GetComponent<Collider2D>() : null;
        Check(playerCollider != null, "the player has a Collider2D");
        if (playerCollider != null)
            Line($"note  player collider: {playerCollider.GetType().Name} isTrigger={playerCollider.isTrigger} " +
                 $"bounds={playerCollider.bounds.size}");

        int triggerSolids = shipSolid.Count(c => c.isTrigger);
        Check(triggerSolids == 0, $"no ship collider is a trigger ({triggerSolids} triggers found)");

        if (spawn != null)
        {
            int ignored = shipSolid.Count(c => Physics2D.GetIgnoreLayerCollision(spawn.gameObject.layer, c.gameObject.layer));
            Check(ignored == 0, $"no ship collider sits on a layer the player ignores ({ignored} ignored)");
        }

        // Box casts out of the walkable spine: ship structure must stop the probe, the corridor must not.
        // The scene also holds non-ship props ("Repair supplies") that already had colliders before this
        // task, so the probes filter to SalvageShuttle colliders and report the raw hit as a note.
        static string HitName(RaycastHit2D h) => h.collider != null ? h.collider.gameObject.name : "nothing";
        static RaycastHit2D FirstShipHit(Vector2 origin, Vector2 size, Vector2 dir, float distance, GameObject root)
        {
            return Physics2D.BoxCastAll(origin, size, 0f, dir, distance)
                .Where(h => h.collider != null &&
                            (h.collider.transform == root.transform || h.collider.transform.IsChildOf(root.transform)))
                .OrderBy(h => h.distance)
                .FirstOrDefault();
        }

        var probeSize = new Vector2(0.5f, 0.5f);
        var rawEast = Physics2D.BoxCast(new Vector2(2.5f, 0.5f), probeSize, 0f, Vector2.right, 4f);
        var shipEast = FirstShipHit(new Vector2(2.5f, 0.5f), probeSize, Vector2.right, 4f, ship);
        Line($"note  east cast raw first hit: {HitName(rawEast)} at {rawEast.distance:F2}");
        Check(shipEast.collider != null,
            $"cast east from the hall centre (2.5,0.5) reaches ship structure ({HitName(shipEast)} at {shipEast.distance:F2})");
        Check(shipEast.distance > 0.9f && shipEast.distance < 2.1f,
            $"the east wall stops the probe at ~1.5 tiles (got {shipEast.distance:F2}; wall face is x=4)");

        var rawDown = Physics2D.BoxCast(new Vector2(2.5f, 0.5f), probeSize, 0f, Vector2.down, 20f);
        var shipDown = FirstShipHit(new Vector2(2.5f, 0.5f), probeSize, Vector2.down, 20f, ship);
        Line($"note  south cast raw first hit: {HitName(rawDown)} at {rawDown.distance:F2} (pre-existing scene prop)");
        Check(shipDown.distance > 7f && shipDown.distance < 9.5f,
            $"with the doors shut the first ship blocker down the spine is the engineering door " +
            $"({HitName(shipDown)} at {shipDown.distance:F2}; the door sits at y=-8.5)");

        var doorBlocks = tiles.Where(t => IsInteriorDoor(t.Family))
            .Select(t => GameObject.Find(t.Label)).Where(g => g != null)
            .Select(g => g.GetComponent<Collider2D>()).Where(c => c != null).ToArray();
        foreach (var c in doorBlocks) c.enabled = false;
        var shipDownOpen = FirstShipHit(new Vector2(2.5f, 0.5f), probeSize, Vector2.down, 20f, ship);
        foreach (var c in doorBlocks) c.enabled = true;
        Check(shipDownOpen.distance > 14f,
            $"with the doors open the spine stays clear down to the sealed secret-door row " +
            $"({HitName(shipDownOpen)} at {shipDownOpen.distance:F2}, secret doors at y=-15.5)");

        var shipSpawn = FirstShipHit(new Vector2(4.5f, -0.5f), probeSize, Vector2.right, 2f, ship);
        Check(shipSpawn.collider != null,
            $"the wall beside the player spawn blocks ({HitName(shipSpawn)} at {shipSpawn.distance:F2})");

        // ---- reachability -------------------------------------------------------------------
        Line("");
        Line("== reachability, flood fill over the real colliders ==");
        var doorColliders = tiles.Where(t => IsInteriorDoor(t.Family))
            .Select(t => GameObject.Find(t.Label)).Where(g => g != null)
            .Select(g => g.GetComponent<Collider2D>()).Where(c => c != null).ToArray();

        var spawnPos = spawn != null ? (Vector2)spawn.transform.position : new Vector2(2.5f, -0.5f);
        var spawnTile = new Vector2(Mathf.Floor(spawnPos.x) + 0.5f, Mathf.Floor(spawnPos.y) + 0.5f);
        Line($"player spawn {spawnPos} -> tile {spawnTile} ({Key(spawnTile)})");

        foreach (var state in new[] { "doors closed", "doors open" })
        {
            if (state == "doors open") foreach (var c in doorColliders) c.enabled = false;
            var blockers = ship.GetComponentsInChildren<Collider2D>(true).Where(c => c.enabled).ToArray();
            var open = Reachable(tiles, blockers, spawnTile);
            bool In(float x, float y) => open.Contains(Key(x, y));

            Line($"[{state}] reachable tiles from spawn: {open.Count}");
            Line(AsciiMap(tiles, open, spawnTile));

            Check(open.Count > 0, $"[{state}] the spawn tile is reachable (not inside a collider)");
            Check(In(2.5f, 0.5f) && In(2.5f, -2.5f), $"[{state}] bridge and upper corridor reachable");
            Check(In(-1.5f, -4.5f) && In(6.5f, -4.5f), $"[{state}] both ends of the cross corridor reachable");
            Check(!In(2.5f, -16.5f), $"[{state}] lower compartment stays sealed behind the solid secret doors");
            if (state == "doors closed")
            {
                Check(!In(1.5f, 4.5f), "[doors closed] cockpit is cut off by the shut salvage door");
                Check(!In(2.5f, -9.5f), "[doors closed] machinery room is cut off by the shut engineering door");
            }
            else
            {
                Check(In(1.5f, 4.5f) && In(3.5f, 4.5f), "[doors open] cockpit reachable through the salvage door");
                Check(In(2.5f, -9.5f) && In(1.5f, -9.5f) && In(3.5f, -9.5f),
                    "[doors open] machinery room reachable through the engineering door");
            }
        }

        foreach (var c in doorColliders) c.enabled = true;

        // ---- report -------------------------------------------------------------------------
        var sb = new StringBuilder();
        sb.AppendLine("SalvageShuttle playability verification (collision + console)");
        sb.AppendLine("scene: " + ScenePath);
        sb.AppendLine("layout: " + LayoutPath);
        sb.AppendLine("generated: " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));
        sb.AppendLine("unity: " + Application.unityVersion);
        sb.AppendLine("result: " + (failures.Count == 0 ? "PASS" : "FAIL") + $" ({failures.Count} failures)");
        sb.AppendLine();
        foreach (var l in log) sb.AppendLine(l);
        if (failures.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("== failures ==");
            foreach (var f in failures) sb.AppendLine(f);
        }

        Directory.CreateDirectory(ScratchDir);
        File.WriteAllText(ReportPath, sb.ToString());
        Debug.Log($"[Play][verify] wrote {ReportPath}: {(failures.Count == 0 ? "PASS" : failures.Count + " FAILURES")}");
        foreach (var f in failures) Debug.LogError("[Play][verify] " + f);
        if (failures.Count > 0)
            throw new Exception($"[Play][verify] {failures.Count} failures - see {ReportPath}");
    }

    /// <summary>Headless entry point: wire, then verify.</summary>
    public static void WireAndVerify()
    {
        Wire();
        Verify();
    }

    /// <summary>
    /// Headless end-to-end: step 1-3 (import + bake + render), step 4 (proximity airlocks) and
    /// step 6-7 (collision + console, verified). Proves the whole salvageshuttle pipeline is
    /// re-runnable in one pass. Needs a real display for the URP render, so no -nographics.
    /// </summary>
    public static void Pipeline()
    {
        SalvageShuttleBake.RunAll();
        ProximityAirlockSetup.WireInteriorAirlocks();
        WireAndVerify();
        Debug.Log("[Play] pipeline complete (bake + render + airlocks + collision + console)");
    }

    /// <summary>
    /// Pipeline plus the airlock floor probe renders (item 2 verification): the probe hides the
    /// closed-door art over both interior airlocks so a missing floor underneath is visible.
    /// </summary>
    public static void PipelineFloorProbe()
    {
        SalvageShuttleBake.RunAll();
        SalvageShuttleBake.RenderAirlockFloorProbe();
        ProximityAirlockSetup.WireInteriorAirlocks();
        WireAndVerify();
        Debug.Log("[Play] pipeline + airlock floor probe complete");
    }

    // ---- helpers --------------------------------------------------------------------------

    static float PolygonArea(Vector2[] pts)
    {
        float a = 0f;
        for (int i = 0; i < pts.Length; i++)
        {
            var p = pts[i];
            var q = pts[(i + 1) % pts.Length];
            a += p.x * q.y - q.x * p.y;
        }

        return Mathf.Abs(a) * 0.5f;
    }

    /// <summary>4-connected flood fill over layout tiles whose centre is not inside a collider.</summary>
    static HashSet<string> Reachable(List<LayoutTile> tiles, Collider2D[] blockers, Vector2 start)
    {
        var present = new HashSet<string>(tiles.Select(t => Key(t.X, t.Y)));
        var solid = new HashSet<string>(
            tiles.Where(t => blockers.Any(c => c.OverlapPoint(new Vector2(t.X, t.Y)))).Select(t => Key(t.X, t.Y)));
        var seen = new HashSet<string>();
        if (!present.Contains(Key(start)) || solid.Contains(Key(start))) return seen;

        var queue = new Queue<Vector2>();
        seen.Add(Key(start));
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var d in new[] { new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1) })
            {
                var n = p + d;
                var k = Key(n);
                if (!present.Contains(k) || solid.Contains(k) || seen.Contains(k)) continue;
                seen.Add(k);
                queue.Enqueue(n);
            }
        }

        return seen;
    }

    static string AsciiMap(List<LayoutTile> tiles, HashSet<string> reachable, Vector2 spawn)
    {
        var sb = new StringBuilder();
        var ys = tiles.Select(t => t.Y).Distinct().OrderByDescending(y => y).ToList();
        var xs = tiles.Select(t => t.X).Distinct().OrderBy(x => x).ToList();
        var family = new Dictionary<string, string>();
        foreach (var t in tiles) family[Key(t.X, t.Y)] = t.Family;

        sb.Append("      ");
        foreach (var x in xs) sb.Append($"{x,4:F1}");
        sb.AppendLine();
        foreach (var y in ys)
        {
            sb.Append($"{y,5:F1} ");
            foreach (var x in xs)
            {
                var k = Key(x, y);
                string cell;
                if (!family.TryGetValue(k, out var fam)) cell = "   .";
                else if (reachable.Contains(k)) cell = "   o";
                else if (IsWalkable(fam)) cell = "   -";
                else if (IsDiagonal(fam)) cell = "   /";
                else if (IsInteriorDoor(fam)) cell = "   D";
                else if (IsPlainWall(fam)) cell = "   #";
                else cell = "   +";
                sb.Append(cell);
            }

            sb.AppendLine();
        }

        sb.AppendLine("      o reachable   - walkable art but unreachable   / diagonal   D door   # wall/window   + equipment");
        sb.AppendLine("      spawn tile " + Key(spawn));
        return sb.ToString().TrimEnd();
    }
}
