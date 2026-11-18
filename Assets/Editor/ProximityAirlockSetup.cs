using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Wires the proximity-operated interior airlocks into the saved HomeShip scene.
///
/// Scope (deliberate, decided from the layout topology rather than by prototype name):
///   ENABLED  - interior passage airlocks that separate two pressurised rooms:
///                AirlockSalvage#128        tile (2.5,  1.5)  bridge <-> cockpit
///                AirlockEngineering#61     tile (2.5, -8.5)  corridor <-> engineering
///   UNTOUCHED- the six hull-side airlocks, which are docking ports to open space and
///              must stay manual/static:
///                AirlockExternalGlassShuttleArrivals#92..#97 at x=-2.5 and x=7.5,
///                y=-3.5/-4.5/-5.5
///   UNTOUCHED- the three SolidReinforcedSecretDoor#62..#64 tiles at y=-15.5 (not airlocks).
///
/// Both interior doors sit in an east-west wall run, so the passage through them runs
/// north-south; that is what <see cref="ProximityAirlock.passageAlongLocalY"/> records.
/// </summary>
public static class ProximityAirlockSetup
{
    const string ScenePath = "Assets/Scenes/HomeShip.unity";
    const string SpriteRoot = "Assets/Sprites/SS14/SalvageShuttle/Doors";

    /// <summary>
    /// Opening trigger in world units. One passage tile is 1.00 world unit here, measured rather than
    /// assumed: the layout grid in Assets/ShipData/salvageshuttle.layout.tsv steps 1.00 per tile in
    /// world x/y, and the salvageshuttle tile art is 32 px at spritePixelsToUnits = 32. The requirement
    /// is that the interior airlocks start opening at most one tile from the doorway, so re-wiring must
    /// write this and can never restore the older 1.5.
    /// </summary>
    const float OneTile = 1.0f;

    /// <summary>Door GameObject name -> art folder under <see cref="SpriteRoot"/>.</summary>
    static readonly (string Door, string Folder)[] InteriorDoors =
    {
        ("AirlockSalvage#128", "Salvage"),
        ("AirlockEngineering#61", "Engineering"),
    };

    /// <summary>Everything that must NOT be automatic, for the verification sweep.</summary>
    static readonly string[] MustStayManual = Enumerable
        .Range(92, 6).Select(i => $"AirlockExternalGlassShuttleArrivals#{i}")
        .Concat(new[] { "SolidReinforcedSecretDoor#62", "SolidReinforcedSecretDoor#63", "SolidReinforcedSecretDoor#64" })
        .ToArray();

    // Upstream RSI nextFrameDelay for the opening/closing states of engineering.rsi and
    // salvage.rsi (identical in both): 0.1 0.1 0.07 0.07 0.07 0.2 -> 0.61 s total.
    static readonly float[] Frames = { 0.1f, 0.1f, 0.07f, 0.07f, 0.07f, 0.2f };

    [MenuItem("Tools/Salvage/4 wire proximity airlocks")]
    public static void WireInteriorAirlocks()
    {
        SalvageShuttleBake.FixImports();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int wired = 0;
        foreach (var (doorName, folder) in InteriorDoors)
        {
            var go = GameObject.Find(doorName);
            if (go == null)
            {
                throw new Exception($"[Airlock] interior door '{doorName}' not found in {ScenePath}");
            }

            var door = go.GetComponent<ProximityAirlock>();
            if (door == null)
            {
                door = go.AddComponent<ProximityAirlock>();
            }

            var dir = $"{SpriteRoot}/{folder}";
            door.panel = go.GetComponentInChildren<SpriteRenderer>(true);
            if (door.panel == null)
            {
                throw new Exception($"[Airlock] {doorName} has no SpriteRenderer child to drive");
            }

            door.openingFrames = LoadFrames($"{dir}/opening.png", "opening");
            door.openSprite = LoadSingle($"{dir}/open.png", "open");
            door.closingFrames = LoadFrames($"{dir}/closing.png", "closing");
            door.closedSprite = LoadSingle($"{dir}/closed.png", "closed");

            if (door.openingFrames == null || door.openingFrames.Length == 0 ||
                door.closingFrames == null || door.closingFrames.Length == 0 ||
                door.openSprite == null || door.closedSprite == null)
            {
                throw new Exception($"[Airlock] {doorName} could not load its sprite states from {dir}");
            }

            door.openingDelays = (float[])Frames.Clone();
            door.closingDelays = (float[])Frames.Clone();
            door.openDistance = OneTile;      // at most one tile from the doorway (was 1.5)
            door.holdDistance = 2.25f;
            door.apertureHalfWidth = 0.8f;
            door.closeDelay = 0.5f;
            door.passageAlongLocalY = true;
            door.sensors = new Transform[0];
            door.logTransitions = true;

            // Keep the art exactly as baked until the player actually approaches.
            door.SetStateImmediate(ProximityAirlock.DoorState.Closed);

            EditorUtility.SetDirty(door);
            Debug.Log($"[Airlock] wired {doorName} at {go.transform.position} " +
                      $"panel={door.panel.name} opening={door.openingFrames.Length}f " +
                      $"closing={door.closingFrames.Length}f");
            wired++;
        }

        // Guard against drift: nothing else may carry an automatic airlock.
        foreach (var g in UnityEngine.Object.FindObjectsByType<ProximityAirlock>(FindObjectsSortMode.None))
        {
            if (InteriorDoors.Any(d => d.Door == g.gameObject.name) || g == null)
            {
                continue;
            }

            Debug.LogWarning($"[Airlock] removing stray ProximityAirlock from {g.gameObject.name}");
            UnityEngine.Object.DestroyImmediate(g, true);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.Refresh();
        Debug.Log($"[Airlock] saved {ScenePath}; {wired} interior airlocks wired");
    }

    /// <summary>Headless entry point: wire + verify, no play mode.</summary>
    public static void WireAndVerifyStatic()
    {
        WireInteriorAirlocks();
        VerifyStatic();
    }

    /// <summary>Prints the door coverage: which doors are automatic and which are not.</summary>
    public static void VerifyStatic()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var doors = UnityEngine.Object.FindObjectsByType<ProximityAirlock>(FindObjectsSortMode.None);

        Debug.Log($"[Airlock][verify] scene={scene.name} automatic airlocks={doors.Length}");
        foreach (var d in doors.OrderBy(d => d.transform.position.y))
        {
            var p = d.transform.position;
            Debug.Log($"[Airlock][verify] ENABLED  {d.gameObject.name} tile=({p.x},{p.y}) " +
                      $"state={d.State} sprite={d.CurrentSpriteName} axis={(d.passageAlongLocalY ? "N/S" : "E/W")} " +
                      $"open<={d.openDistance} hold>={d.holdDistance} delay={d.closeDelay}s");
        }

        int manualFound = 0;
        foreach (var name in MustStayManual)
        {
            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning($"[Airlock][verify] expected-but-missing GameObject {name}");
                continue;
            }

            var comp = go.GetComponent<ProximityAirlock>();
            if (comp != null)
            {
                Debug.LogError($"[Airlock][verify] REGRESSION: {name} must stay manual but has ProximityAirlock");
            }
            else
            {
                var p = go.transform.position;
                Debug.Log($"[Airlock][verify] UNTOUCHED {name} tile=({p.x},{p.y})");
                manualFound++;
            }
        }

        Debug.Log($"[Airlock][verify] non-automatic doors confirmed unchanged: {manualFound}/{MustStayManual.Length}");
    }

    static Sprite[] LoadFrames(string path, string stem)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .Where(s => s.name.StartsWith(stem + "_", StringComparison.Ordinal))
            .OrderBy(s => FrameIndex(s.name, stem))
            .ToArray();
    }

    static Sprite LoadSingle(string path, string stem)
    {
        // Either a single-sprite import ("open") or the first cell of a sheet ("open_0").
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(s => s.name == stem ? 0 : FrameIndex(s.name, stem))
            .FirstOrDefault();
    }

    static int FrameIndex(string spriteName, string stem)
    {
        var suffix = spriteName.Substring(stem.Length).TrimStart('_');
        return int.TryParse(suffix, out var i) ? i : 0;
    }

    /// <summary>Static wiring report for one door, used by the runtime harness.</summary>
    public static List<ProximityAirlock> CollectAutomaticDoors()
    {
        return UnityEngine.Object.FindObjectsByType<ProximityAirlock>(FindObjectsSortMode.None).ToList();
    }
}
