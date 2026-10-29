using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InventorySandboxSetup
{
    [MenuItem("NTExpedition/Create inventory sandbox")]
    public static void Create()
    {
        const string folder = "Assets/InventorySandbox";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "InventorySandbox");
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var main = new GameObject("Inventory sandbox");
        var bag = main.AddComponent<InventoryContainer>();
        var demo = main.AddComponent<InventorySandbox>();
        demo.bag = bag;
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
    public static void BuildHomeShip()
    {
        var report = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/HomeShip.unity" }, "Build/NTExpedition.x86_64", BuildTarget.StandaloneLinux64, BuildOptions.Development);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new System.Exception("HomeShip build failed: " + report.summary.result);
        Debug.Log("HOMESHIP_BUILD_PASS");
    }
    public static void ConfigureBackpackAndSatchel()
    {
        const string spritePath = "Assets/ThirdParty/FrontierStation14/Satchel/basic-satchel.png";
        const string definitionPath = "Assets/Resources/WorldItems/BasicSatchelDefinition.asset";
        const string scenePath = "Assets/Scenes/HomeShip.unity";

        AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var satchel = MakeItem(definitionPath, "Basic satchel", new Vector2Int(2, 2), null);
        var satchelData = new SerializedObject(satchel);
        satchelData.FindProperty("itemIcon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        satchelData.FindProperty("itemWeight").floatValue = 1f;
        satchelData.ApplyModifiedPropertiesWithoutUndo();

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var handUi = Object.FindFirstObjectByType<HandUI>();
        var inventory = Object.FindFirstObjectByType<InventorySandbox>();
        if (handUi == null || handUi.LeftSlot == null || inventory == null)
            throw new System.Exception("HomeShip is missing its hand UI or storage interface.");

        var uiRoot = handUi.LeftSlot.transform.parent;
        var status = uiRoot.Find("LeftHandStatusBar");
        if (status == null) status = uiRoot.Find("LeftHandSlot (1)");
        if (status == null) throw new System.Exception("HomeShip is missing the left-hand status bar.");
        status.name = "LeftHandStatusBar";

        var backpack = uiRoot.Find("BackpackSlot");
        if (backpack == null)
        {
            var go = new GameObject("BackpackSlot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backpack = go.transform;
            backpack.SetParent(uiRoot, false);
        }
        var backpackRect = (RectTransform)backpack;
        backpackRect.anchorMin = new Vector2(.5f, 0f);
        backpackRect.anchorMax = new Vector2(.5f, 0f);
        // The status bar has a 12 px end cap outside its main RectTransform.
        // Keep the backpack clear of that visible cap, not only the bar's body.
        backpackRect.anchoredPosition = new Vector2(-156f, 21f);
        backpackRect.sizeDelta = new Vector2(32f, 32f);
        backpackRect.pivot = new Vector2(.5f, .5f);
        var backpackImage = backpack.GetComponent<Image>();
        backpackImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/SS14/HandUI/back.png");
        backpackImage.raycastTarget = false;
        backpackImage.preserveAspect = true;

        var handUiData = new SerializedObject(handUi);
        handUiData.FindProperty("backpackSlot").objectReferenceValue = backpackImage;
        handUiData.ApplyModifiedPropertiesWithoutUndo();
        var inventoryData = new SerializedObject(inventory);
        var items = inventoryData.FindProperty("startingItems");
        items.arraySize = 1;
        items.GetArrayElementAtIndex(0).objectReferenceValue = satchel;
        inventoryData.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("BACKPACK_SATCHEL_SETUP_PASS");
    }

    public static void ConfigureBaseballBat()
    {
        const string spritePath = "Assets/ThirdParty/FrontierStation14/BaseballBat/baseball-bat.png";
        const string definitionPath = "Assets/Resources/WorldItems/BaseballBatDefinition.asset";
        const string prefabPath = "Assets/Resources/WorldItems/BaseballBat.prefab";

        AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 32;
        var textureSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(textureSettings);
        textureSettings.spriteAlignment = (int)SpriteAlignment.Custom;
        // Frontier's icon points northeast; pivot at the lower-left handle.
        textureSettings.spritePivot = new Vector2(.08f, .08f);
        importer.SetTextureSettings(textureSettings);
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        var bat = MakeItem(definitionPath, "Baseball bat", new Vector2Int(1, 3), null);
        var data = new SerializedObject(bat);
        data.FindProperty("itemIcon").objectReferenceValue = sprite;
        data.FindProperty("itemWeight").floatValue = 1.2f;
        data.FindProperty("isMeleeWeapon").boolValue = true;
        data.FindProperty("meleeDamage").floatValue = 18f;
        data.FindProperty("meleeRange").floatValue = 1.55f;
        data.FindProperty("meleeCooldown").floatValue = .55f;
        data.FindProperty("meleeSwingArc").floatValue = 120f;
        data.FindProperty("meleeSwingDuration").floatValue = .24f;
        data.FindProperty("meleeKnockback").floatValue = .8f;
        data.FindProperty("meleeSpriteAngle").floatValue = 45f;
        data.ApplyModifiedPropertiesWithoutUndo();

        var root = new GameObject("BaseballBat");
        var renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 5;
        var collider = root.AddComponent<CapsuleCollider2D>();
        collider.isTrigger = true;
        collider.direction = CapsuleDirection2D.Vertical;
        collider.size = new Vector2(.45f, 1.65f);
        root.AddComponent<ItemComponent>().Initialize(bat);
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("BASEBALL_BAT_SETUP_PASS");
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
