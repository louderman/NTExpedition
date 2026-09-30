using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// AI-built editor infrastructure, annotated for study. This does NOT slice textures.
// Select a sliced PNG, then Tools > RSI > Generate Animation Clips.
// Assumption: Animator and SpriteRenderer are on the SAME GameObject (binding path = "").
public static class RsiAnimationGenerator
{
    private static readonly string[] DirectionNames = { "south", "north", "east", "west" };

    [MenuItem("Tools/RSI/Generate Animation Clips")]
    private static void GenerateSelected()
    {
        // Check both the selected asset's type and assign it to a local variable.
        if (!(Selection.activeObject is Texture2D texture))
        {
            Debug.LogError("Select the sliced PNG texture, not an individual Sprite.");
            return;
        }

        string texturePath = AssetDatabase.GetAssetPath(texture);
        try
        {
            string[] paths = Generate(texturePath);
            foreach (string path in paths)
                Debug.Log($"Created RSI animation: {path}");
        }
        catch (Exception error) // Show validation failures in Unity's Console.
        {
            Debug.LogError($"RSI animation generation failed: {error.Message}");
        }
    }

    // Public so the isolated Unity test can exercise exactly the same implementation.
    public static string[] Generate(string texturePath)
    {
        if (!texturePath.StartsWith("Assets/", StringComparison.Ordinal) ||
            !string.Equals(Path.GetExtension(texturePath), ".png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Choose a PNG asset inside Assets/.");

        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null || importer.spriteImportMode != SpriteImportMode.Multiple)
            throw new InvalidDataException("The PNG must already be sliced (Sprite Mode: Multiple). No slicing is performed.");

        string folder = Path.GetDirectoryName(texturePath).Replace('\\', '/');
        string stateName = Path.GetFileNameWithoutExtension(texturePath); // golem2.png -> golem2.
        string metadataPath = folder + "/source-meta.json";
        // Accept the original RSI filename too; source-meta.json takes precedence when both exist.
        if (!File.Exists(metadataPath))
            metadataPath = folder + "/meta.json";
        if (!File.Exists(metadataPath))
            throw new InvalidDataException("No source-meta.json or meta.json beside the PNG.");

        JObject metadata = JObject.Parse(File.ReadAllText(metadataPath)); // JSON text -> searchable fields.
        JArray states = metadata["states"] as JArray; // as returns null if this isn't a JSON array.
        if (states == null)
            throw new InvalidDataException("Metadata has no states array.");

        JToken state = null;
        foreach (JToken candidate in states) // Explicit search instead of a LINQ expression.
        {
            if ((string)candidate["name"] == stateName)
            {
                state = candidate;
                break; // Matching state found: stop searching.
            }
        }
        if (state == null)
            throw new InvalidDataException($"Metadata has no state named '{stateName}'.");

        int directionCount = state["directions"] == null ? 1 : state["directions"].Value<int>();
        if (directionCount != 1 && directionCount != 4)
            throw new InvalidDataException("Only one-direction or four-direction RSI states are supported.");

        JArray delayGroups = state["delays"] as JArray;
        if (delayGroups == null)
            throw new InvalidDataException("No animated frame delays. Use SpriteRenderer directly for static directional sprites.");
        if (delayGroups.Count != directionCount)
            throw new InvalidDataException("The number of delay groups does not match the direction count.");

        // Validate every direction before creating any files. Each duration belongs to its own frame.
        var durations = new List<float[]>();
        int expectedSpriteCount = 0;
        for (int direction = 0; direction < directionCount; direction++)
        {
            JArray delays = delayGroups[direction] as JArray;
            if (delays == null || delays.Count < 2)
                throw new InvalidDataException("Each direction needs at least two frames. Use SpriteRenderer directly for single-frame directions.");

            float[] frameDurations = new float[delays.Count];
            for (int frame = 0; frame < delays.Count; frame++)
            {
                float duration = delays[frame].Value<float>();
                if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f)
                    throw new InvalidDataException("Frame durations must be finite and greater than zero.");
                frameDurations[frame] = duration;
            }
            durations.Add(frameDurations);
            expectedSpriteCount += frameDurations.Length;
        }

        var sprites = new List<Sprite>();
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(texturePath); // Includes the texture AND sprite sub-assets.
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite)
                sprites.Add(sprite); // Ignore non-Sprite assets.
        }
        sprites.Sort(CompareSprites); // Explicit geometry order; Unity's asset-return order is not guaranteed.
        if (sprites.Count != expectedSpriteCount)
            throw new InvalidDataException($"Metadata expects {expectedSpriteCount} sprites, but the PNG has {sprites.Count} slices.");

        // Keep generated files separate from existing hand-made clips.
        string outputFolder = folder + "/RSIAnimations";
        string[] outputPaths = new string[directionCount];
        for (int direction = 0; direction < directionCount; direction++)
        {
            string directionName = directionCount == 4 ? DirectionNames[direction] : "single";
            outputPaths[direction] = outputFolder + "/" + stateName + "_" + directionName + ".anim";
            if (File.Exists(outputPaths[direction]) || File.Exists(outputPaths[direction] + ".meta"))
                throw new IOException($"Refusing to overwrite {outputPaths[direction]}. Move existing generated clips before rerunning.");
        }

        var clips = new List<AnimationClip>();
        var writtenPaths = new List<string>(); // Used to remove only this invocation's files if saving fails.
        try
        {
            int spriteIndex = 0; // Continues across direction groups in the sorted sprite list.
            for (int direction = 0; direction < directionCount; direction++)
            {
                var clip = new AnimationClip();
                clips.Add(clip); // Track it immediately, so failure cleanup can release it.
                clip.name = Path.GetFileNameWithoutExtension(outputPaths[direction]);
                // Unity includes one sample interval after its final sprite key.
                // A higher rate is needed only when a supplied frame is shorter than one 60 Hz sample.
                float smallestDuration = durations[direction][0];
                foreach (float duration in durations[direction])
                    smallestDuration = Mathf.Min(smallestDuration, duration);
                clip.frameRate = Mathf.Max(60f, Mathf.Ceil(1f / smallestDuration));
                clip.legacy = false; // Intended for Animator, not the legacy Animation component.

                var keys = new List<ObjectReferenceKeyframe>();
                float elapsed = 0f;
                Sprite lastSprite = null;
                foreach (float duration in durations[direction])
                {
                    Sprite sprite = sprites[spriteIndex];
                    spriteIndex++;
                    // Record START time first, THEN advance by how long this sprite lasts.
                    keys.Add(new ObjectReferenceKeyframe { time = elapsed, value = sprite });
                    elapsed += duration;
                    lastSprite = sprite;
                }
                if (float.IsInfinity(elapsed))
                    throw new InvalidDataException("Total animation duration is too large.");

                // Hold the last pose until the cycle ends. Unity extends the final key by one sample,
                // so put this duplicate last-pose key one sample BEFORE the desired end, not at the end.
                float endMarkerTime = elapsed - (1f / clip.frameRate);
                float lastStartTime = keys[keys.Count - 1].time;
                if (endMarkerTime > lastStartTime)
                    keys.Add(new ObjectReferenceKeyframe { time = endMarkerTime, value = lastSprite });
                EditorCurveBinding binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray()); // Sprite references, not a float curve.

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.startTime = 0f;
                settings.stopTime = elapsed; // Use the metadata's complete cycle, not just the final frame's start time.
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }

            if (!AssetDatabase.IsValidFolder(outputFolder))
                AssetDatabase.CreateFolder(folder, "RSIAnimations");
            for (int direction = 0; direction < clips.Count; direction++)
            {
                writtenPaths.Add(outputPaths[direction]); // Register the target before attempting its write.
                AssetDatabase.CreateAsset(clips[direction], outputPaths[direction]);
                if (AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPaths[direction]) == null)
                    throw new IOException($"Unity did not create {outputPaths[direction]}.");
            }
            AssetDatabase.SaveAssets();
            return outputPaths;
        }
        catch
        {
            foreach (string path in writtenPaths)
                AssetDatabase.DeleteAsset(path); // Never delete pre-existing clips; preflight rejected those above.
            foreach (AnimationClip clip in clips)
            {
                if (clip != null && !AssetDatabase.Contains(clip))
                    UnityEngine.Object.DestroyImmediate(clip); // Release unsaved editor objects after a failed attempt.
            }
            throw; // Let the caller report the original failure.
        }
    }

    private static int CompareSprites(Sprite first, Sprite second)
    {
        // Rect Y increases upwards: larger Y sorts first (top row), then smaller X first (left column).
        int rowOrder = second.rect.y.CompareTo(first.rect.y);
        if (rowOrder != 0)
            return rowOrder;
        return first.rect.x.CompareTo(second.rect.x);
    }
}
