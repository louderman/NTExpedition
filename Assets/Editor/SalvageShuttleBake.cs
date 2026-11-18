using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Recreates the SS14 "salvageshuttle" map as sprites in Assets/Scenes/HomeShip.unity.
/// All SS14 logic (icon smoothing, RSI direction cells, rotations, sorting) is pre-resolved by
/// build_layout.py; this script only slices textures and places renderers.
/// </summary>
public static class SalvageShuttleBake
{
    const string ScenePath = "Assets/Scenes/HomeShip.unity";
    const string LayoutPath = "Assets/ShipData/salvageshuttle.layout.tsv";
    const string ImportRoot = "Assets/Sprites/SS14/SalvageShuttle";
    const string RenderDir = "Assets/ShipData/renders";

    [MenuItem("Tools/Salvage/Run all (import + bake + render)")]
    public static void RunAll()
    {
        FixImports();
        Bake();
        Render("full", new Vector2(2.5f, -6f), 13f, 720, 1080);
        Render("interior", new Vector2(2.5f, -1.5f), 6f, 900, 720);
        AssetDatabase.Refresh();
        Debug.Log("[Salvage] RunAll complete");
    }

    /// <summary>Headless entry point: import + bake only (no GPU render).</summary>
    public static void ImportAndBake()
    {
        FixImports();
        Bake();
        AssetDatabase.Refresh();
        Debug.Log("[Salvage] ImportAndBake complete");
    }

    /// <summary>Headless entry point: render only (needs a graphics display).</summary>
    public static void RenderOnly()
    {
        RenderScreenshots();
        Debug.Log("[Salvage] RenderOnly complete");
    }

    [MenuItem("Tools/Salvage/1 fix imports")]
    public static void FixImports()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ImportRoot });
        int n = 0;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            if (tex == null) { Debug.LogWarning("[Salvage] cannot read " + path); continue; }

            int w = tex.width, h = tex.height;
            var stem = Path.GetFileNameWithoutExtension(path);
            int cols = Mathf.Max(1, w / 32), rows = Mathf.Max(1, h / 32);
            var metas = new List<SpriteMetaData>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var sm = new SpriteMetaData
                    {
                        name = stem + "_" + (r * cols + c),
                        rect = new Rect(c * 32, h - (r + 1) * 32, 32, 32),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f),
                    };
                    metas.Add(sm);
                }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = 32f;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.spritesheet = metas.ToArray();
            ti.SaveAndReimport();
            n++;
        }
        AssetDatabase.Refresh();
        Debug.Log($"[Salvage] sliced {n} textures under {ImportRoot}");
    }

    [MenuItem("Tools/Salvage/2 bake layout into scene")]
    public static void Bake()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var gridGo = GameObject.Find("Grid");
        if (gridGo == null) throw new Exception("[Salvage] Grid GameObject not found in " + ScenePath);

        var old = GameObject.Find("SalvageShuttle");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        int cleared = 0;
        foreach (var tm in gridGo.GetComponentsInChildren<Tilemap>(true))
        {
            cleared += tm.GetUsedTilesCount();
            tm.ClearAllTiles();
            EditorUtility.SetDirty(tm);
        }

        var root = new GameObject("SalvageShuttle");
        root.transform.SetParent(gridGo.transform, false);

        var lines = File.ReadAllLines(LayoutPath)
            .Where(l => l.Length > 0 && !l.StartsWith("#")).ToArray();
        var goByLabel = new Dictionary<string, GameObject>();
        var missing = new List<string>();
        int placed = 0;
        foreach (var line in lines)
        {
            var f = line.Split('\t');
            if (f.Length < 7) { missing.Add("bad row: " + line); continue; }
            string label = f[0], spritePath = f[1], cell = f[2];
            float px = float.Parse(f[3], CultureInfo.InvariantCulture);
            float py = float.Parse(f[4], CultureInfo.InvariantCulture);
            float rot = float.Parse(f[5], CultureInfo.InvariantCulture);
            int order = int.Parse(f[6], CultureInfo.InvariantCulture);

            // spritePath is either a plain texture file (Tiles/*.png) or a folder of
            // <stem>.png sheets (all RSI-backed sprites); cell is always "<stem>_<cellIndex>".
            var pngPath = spritePath.EndsWith(".png")
                ? spritePath
                : spritePath + "/" + cell.Substring(0, cell.LastIndexOf('_')) + ".png";
            var sp = AssetDatabase.LoadAllAssetsAtPath(pngPath)
                .OfType<Sprite>().FirstOrDefault(s => s.name == cell);
            if (sp == null) { missing.Add(pngPath + ":" + cell); continue; }

            if (!goByLabel.TryGetValue(label, out var go))
            {
                go = new GameObject(label);
                go.transform.SetParent(root.transform, false);
                go.transform.position = new Vector3(px, py, 0f);
                goByLabel[label] = go;
            }
            var child = new GameObject(cell);
            child.transform.SetParent(go.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = order;
            placed++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"[Salvage] cleared {cleared} placeholder tiles; {goByLabel.Count} entities, " +
                  $"{placed} sprite layers placed; {missing.Count} problems");
        if (missing.Count > 0)
            Debug.LogWarning("[Salvage] PROBLEMS: " + string.Join(" | ", missing.Distinct().Take(30)));
    }

    [MenuItem("Tools/Salvage/3 render screenshots")]
    public static void RenderScreenshots()
    {
        Render("full", new Vector2(2.5f, -6f), 13f, 720, 1080);
        Render("interior", new Vector2(2.5f, -1.5f), 6f, 900, 720);
        AssetDatabase.Refresh();
    }

    static void Render(string tag, Vector2 center, float orthoSize, int w, int h)
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory(RenderDir);
        RenderCamera($"{RenderDir}/salvage_{tag}.png", center, orthoSize, w, h);
    }

    /// <summary>
    /// Camera + RenderTexture capture to a png path. Assumes the scene to capture is already open -
    /// it does NOT reload it, so callers can hide/show renderers in memory first.
    /// </summary>
    static void RenderCamera(string assetPath, Vector2 center, float orthoSize, int w, int h)
    {
        var dir = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var camGo = new GameObject("__salvage_tmp_cam");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;
        cam.transform.position = new Vector3(center.x, center.y, -100f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.035f, 0.04f, 0.07f, 1f);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1000f;

        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        cam.targetTexture = rt;

        bool rendered = false;
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
        {
            var req = new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(cam, req))
            {
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(cam, req);
                rendered = true;
            }
        }
        if (!rendered)
            cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(camGo);
        Debug.Log("[Salvage] wrote " + assetPath);
    }

    /// <summary>
    /// Verification probe for the airlock floor: in the baked scene both interior airlock doors are
    /// drawn closed, so an ordinary render hides the tiles behind them. This hides the door art,
    /// captures the tiles underneath (where a missing floor shows up as background black), then
    /// restores every renderer and never saves the scene.
    /// </summary>
    public static void RenderAirlockFloorProbe()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var doors = new[] { "AirlockEngineering#61", "AirlockSalvage#128" };
        var found = new List<string>();
        var hidden = new List<SpriteRenderer>();
        foreach (var label in doors)
        {
            var go = GameObject.Find(label);
            if (go == null) { Debug.LogWarning("[Salvage] floor probe: no GameObject '" + label + "'"); continue; }
            found.Add(label);
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.enabled) { sr.enabled = false; hidden.Add(sr); }
        }

        RenderCamera($"{RenderDir}/airlock/airlock_08_engineering_floor_probe.png", new Vector2(2.5f, -8.5f), 3f, 640, 640);
        RenderCamera($"{RenderDir}/airlock/airlock_09_salvage_floor_probe.png", new Vector2(2.5f, 1.5f), 3f, 640, 640);

        foreach (var sr in hidden) if (sr != null) sr.enabled = true;
        Debug.Log($"[Salvage] airlock floor probe: door art hidden on [{string.Join(", ", found)}]; " +
                  $"{hidden.Count} renderer(s) restored; 2 probe renders written");
    }
}
