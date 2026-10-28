using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InventorySandboxSetup
{
    [MenuItem("NTExpedition/Create inventory sandbox")]
    public static void Create()
    {
        const string folder = "Assets/InventorySandbox";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "InventorySandbox");
        var line = MakeItem(folder + "/Tool.asset", "Crowbar", new Vector2Int(1,2), null);
        var square = MakeItem(folder + "/Kit.asset", "Wrench", new Vector2Int(1,2), null);
        var ell = MakeItem(folder + "/Corner.asset", "Screwdriver", new Vector2Int(1,2), null);
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var main = new GameObject("Inventory sandbox");
        var bag = main.AddComponent<InventoryContainer>();
        var demo = main.AddComponent<InventorySandbox>();
        demo.bag = bag; demo.samples = new[] {line, square, ell};
        EditorSceneManager.SaveScene(scene, folder + "/InventorySandbox.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("Inventory sandbox generated: " + folder + "/InventorySandbox.unity");
    }
    public static void BuildSandbox()
    {
        var report = BuildPipeline.BuildPlayer(new[] { "Assets/InventorySandbox/InventorySandbox.unity" }, "Build/InventorySandbox.x86_64", BuildTarget.StandaloneLinux64, BuildOptions.None);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("Sandbox build failed: " + report.summary.result);
        Debug.Log("INVENTORY_SANDBOX_BUILD_PASS");
    }
    static ItemDefinition MakeItem(string path, string name, Vector2Int dimensions, Vector2Int[] footprint)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null) { item = ScriptableObject.CreateInstance<ItemDefinition>(); AssetDatabase.CreateAsset(item, path); }
        var data = new SerializedObject(item);
        data.FindProperty("itemName").stringValue = name;
        data.FindProperty("itemDimensions").vector2IntValue = dimensions;
        var cells = data.FindProperty("occupiedCells"); cells.arraySize = footprint == null ? 0 : footprint.Length;
        if (footprint != null) for (int i=0; i<footprint.Length; i++) cells.GetArrayElementAtIndex(i).vector2IntValue = footprint[i];
        data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(item); return item;
    }
}
