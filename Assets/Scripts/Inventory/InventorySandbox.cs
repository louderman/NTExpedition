using System.Collections;
using UnityEngine;

// Temporary sample storage shown in HomeShip.
public class InventorySandbox : MonoBehaviour
{
    public InventoryContainer bag;
    [SerializeField] ItemDefinition[] startingItems;
    ItemComponent dragging;
    int dragRotation;
    Vector2Int dragAnchor;
    const float Cell = 32f;
    const float Gap = 0f;
    Texture2D tileTexture, pieceTexture, sidebarTop, sidebarMid, sidebarBottom, exitIcon;
    HandSystem handSystem;
    Texture2D LoadTexture(string name)
    {
        var texture = Resources.Load<Texture2D>("FrontierInventory/" + name);
        if (texture != null) texture.filterMode = FilterMode.Point;
        return texture;
    }
    static readonly Color Frame = new Color(.07f,.07f,.08f,.97f);
    static readonly Color Tile = new Color(.17f,.17f,.19f,1f);
    static readonly Color ItemTile = new Color(.29f,.30f,.32f,1f);
    static readonly Color Border = new Color(.04f,.04f,.05f,1f);

    void Start()
    {
        handSystem = Object.FindFirstObjectByType<HandSystem>();
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--inventory-self-test") >= 0)
        {
            var definition = Resources.Load<ItemDefinition>("WorldItems/WaterBottleDefinition");
            var obj = new GameObject("integration-test-item");
            var item = obj.AddComponent<ItemComponent>(); item.Initialize(definition);
            bool ok = definition != null && definition.ItemName == "Water bottle"
                && definition.ItemIcon != null && definition.ItemDimensions == new Vector2Int(1, 2)
                && definition.GetFootprint().Length == 2 && IsEmpty();
            ok &= bag.TryPlace(item, new Vector2Int(0, 0), 0) && bag.GetItem(0, 1) == item && item.Container == bag;
            item.Rotate();
            ok &= item.Rotation == 1 && bag.GetItem(1, 0) == item && bag.GetItem(0, 1) == null;
            ok &= !bag.TryPlace(item, new Vector2Int(5, 3), 0) && bag.GetItem(1, 0) == item;
            ok &= bag.Remove(item) && item.Container == null && bag.GetItem(0, 0) == null;
            Debug.Log(ok ? "INVENTORY_INTEGRATION_PASS" : "INVENTORY_INTEGRATION_FAIL");
            Application.Quit(ok ? 0 : 1);
            return;
        }
        AddStartingItems();
        tileTexture = LoadTexture("tile_empty_opaque"); pieceTexture = LoadTexture("piece_center");
        sidebarTop = LoadTexture("sidebar_top"); sidebarMid = LoadTexture("sidebar_mid");
        sidebarBottom = LoadTexture("sidebar_bottom"); exitIcon = LoadTexture("exit");
        if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--inventory-screenshot") >= 0) StartCoroutine(Capture());
    }
    void AddStartingItems()
    {
        if (bag == null || startingItems == null) return;
        foreach (var definition in startingItems)
        {
            if (definition == null) continue;
            var obj = new GameObject(definition.ItemName + " (stored)");
            obj.transform.SetParent(transform, false);
            var item = obj.AddComponent<ItemComponent>();
            item.Initialize(definition);
            if (!bag.TryAutoPlace(item))
            {
                Debug.LogError("Could not place starting item in storage: " + definition.ItemName);
                Destroy(obj);
            }
        }
    }
    bool IsEmpty()
    {
        if (bag == null) return false;
        var dimensions = bag.ContainerDimensions;
        for (int y = 0; y < dimensions.y; y++) for (int x = 0; x < dimensions.x; x++)
            if (bag.GetItem(x, y) != null) return false;
        return true;
    }
    public bool TryUseBag()
    {
        if (handSystem == null) handSystem = Object.FindFirstObjectByType<HandSystem>();
        return handSystem != null && handSystem.TryStoreActiveItem(bag);
    }
    IEnumerator Capture()
    {
        yield return new WaitForSecondsRealtime(2);
        ScreenCapture.CaptureScreenshot("inventory-homeship.png");
        yield return new WaitForSecondsRealtime(1);
        Application.Quit();
    }
    static void Fill(Rect r, Color color)
    {
        Color previous = GUI.color; GUI.color = color; GUI.DrawTexture(r, Texture2D.whiteTexture); GUI.color = previous;
    }
    public static bool PointerOverStorage(Vector2 screenPosition)
    {
        var panel = Object.FindFirstObjectByType<InventorySandbox>();
        if (panel == null || !panel.isActiveAndEnabled || panel.bag == null) return false;
        var dims = panel.bag.ContainerDimensions;
        return new Rect(24, 70, dims.x * Cell + 32, dims.y * Cell + 24)
            .Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
    }
    void OnGUI()
    {
        if (bag == null) return;
        var dimensions = bag.ContainerDimensions;
        float width = dimensions.x * Cell, height = dimensions.y * Cell;
        // A compact SS14-inspired floating storage, no per-cell buttons.
        Rect panel = new Rect(24, 70, width + 32, height + 24);
        Rect grid = new Rect(panel.x + 32, panel.y + 24, width, height);
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && panel.Contains(e.mousePosition)
            && handSystem != null && handSystem.HeldItem(handSystem.CurrentHandSlot) != null)
        {
            if (!TryUseBag()) Debug.Log("Inventory full: held item was not stored");
            e.Use();
            return;
        }
        Vector2Int hovered = new Vector2Int(Mathf.FloorToInt((e.mousePosition.x-grid.x)/Cell), Mathf.FloorToInt((e.mousePosition.y-grid.y)/Cell));
        bool inGrid = grid.Contains(e.mousePosition);
        if (e.type == EventType.MouseDown && e.button == 0 && inGrid)
        {
            var item = bag.GetItem(hovered.x, hovered.y);
            if (item != null) { dragging = item; dragRotation = item.Rotation; dragAnchor = hovered - item.GridPosition; e.Use(); }
        }
        else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.R && dragging != null)
        {
            dragRotation = (dragRotation + 1) % 4; dragAnchor = Vector2Int.zero; e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && dragging != null)
        {
            if (inGrid) bag.TryPlace(dragging, hovered - dragAnchor, dragRotation);
            dragging = null; e.Use();
        }
        if (e.type != EventType.Repaint) return;
        Fill(panel, Border); Fill(new Rect(panel.x+3,panel.y+3,panel.width-6,panel.height-6), Frame);
        var title = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.8f,.8f,.82f) } };
        GUI.Label(new Rect(panel.x+12,panel.y+2,panel.width-24,22), "Storage", title);
        for (int y=0; y<dimensions.y; y++)
        {
            var sidebar = y == 0 ? sidebarTop : (y == dimensions.y - 1 ? sidebarBottom : sidebarMid);
            if (sidebar != null) GUI.DrawTexture(new Rect(panel.x, grid.y+y*Cell, Cell, Cell), sidebar);
        }
        for (int y=0; y<dimensions.y; y++) for (int x=0; x<dimensions.x; x++)
        {
            Rect r = new Rect(grid.x+x*Cell,grid.y+y*Cell,Cell-Gap,Cell-Gap);
            if (tileTexture != null) GUI.DrawTexture(r, tileTexture); else Fill(r, Tile);
            var item = bag.GetItem(x,y);
            if (item != null && item != dragging)
            {
                if (pieceTexture != null) GUI.DrawTexture(r, pieceTexture); else Fill(r, ItemTile);
            }
        }
        // Draw one visual label/icon per item, not once per occupied cell.
        var drawn = new System.Collections.Generic.HashSet<ItemComponent>();
        for (int y=0; y<dimensions.y; y++) for (int x=0; x<dimensions.x; x++)
        {
            var item = bag.GetItem(x,y);
            if (item == null || item == dragging || !drawn.Add(item)) continue;
            DrawItem(item, grid, item.GridPosition, item.Rotation);
        }
        if (dragging != null)
        {
            var pos = hovered - dragAnchor;
            bool valid = inGrid && CanPlace(dragging, pos, dragRotation);
            foreach (var cell in InventoryGrid<ItemComponent>.Rotate(dragging.Definition.GetFootprint(),dragRotation))
            {
                var r = new Rect(grid.x+(pos.x+cell.X)*Cell+2,grid.y+(pos.y+cell.Y)*Cell+2,Cell-Gap-4,Cell-Gap-4);
                Fill(r, valid ? new Color(.26f,.48f,.35f,.9f) : new Color(.55f,.21f,.21f,.9f));
            }
            DrawItem(dragging, grid, pos, dragRotation);
        }
    }
    bool CanPlace(ItemComponent item, Vector2Int position, int rotation)
    {
        var shape = InventoryGrid<ItemComponent>.Rotate(item.Definition.GetFootprint(),rotation);
        var offsets = new Vector2Int[shape.Length];
        for (int i=0; i<shape.Length; i++) offsets[i] = new Vector2Int(shape[i].X,shape[i].Y);
        return bag.CanFit(item,position,offsets);
    }
    void DrawItem(ItemComponent item, Rect grid, Vector2Int pos, int rotation)
    {
        var shape = InventoryGrid<ItemComponent>.Rotate(item.Definition.GetFootprint(),rotation);
        int w=0,h=0; foreach(var p in shape) { w=Mathf.Max(w,p.X+1); h=Mathf.Max(h,p.Y+1); }
        Rect r = new Rect(grid.x+pos.x*Cell, grid.y+pos.y*Cell, w*Cell-Gap, h*Cell-Gap);
        if (item.Definition.ItemIcon != null)
        {
            var sprite = item.Definition.ItemIcon; var t = sprite.texture; var sr = sprite.textureRect;
            float scale = Mathf.Min(r.width / sr.width, r.height / sr.height);
            var iconRect = new Rect(r.center.x - sr.width * scale / 2f, r.center.y - sr.height * scale / 2f,
                sr.width * scale, sr.height * scale);
            GUI.DrawTextureWithTexCoords(iconRect, t, new Rect(sr.x/t.width,sr.y/t.height,sr.width/t.width,sr.height/t.height));
        }
        else
        {
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize=12, normal = { textColor = Color.white } };
            GUI.Label(r,item.Definition.ItemName,style);
        }
    }
}
