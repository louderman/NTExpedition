using UnityEngine;
using UnityEngine.UI;

public class HandUI : MonoBehaviour
{
    [SerializeField] private Image leftHighlight;
    [SerializeField] private Image rightHighlight;
    [SerializeField] private HandSystem handSystem;
    Image leftItemIcon, rightItemIcon;
    public Image LeftItemIcon => leftItemIcon;
    public Image RightItemIcon => rightItemIcon;

    void OnEnable()
    {
        if (handSystem == null || leftHighlight == null || rightHighlight == null) return;
        if (leftItemIcon == null) leftItemIcon = MakeIcon(leftHighlight.transform, "Left held item");
        if (rightItemIcon == null) rightItemIcon = MakeIcon(rightHighlight.transform, "Right held item");
        handSystem.HandChanged += DisplayHand;
        handSystem.HeldItemChanged += DisplayItem;
        DisplayHand(handSystem.CurrentHandSlot);
        DisplayItem(HandSystem.HandSlot.Left, handSystem.HeldItem(HandSystem.HandSlot.Left));
        DisplayItem(HandSystem.HandSlot.Right, handSystem.HeldItem(HandSystem.HandSlot.Right));
    }
    void OnDisable()
    {
        if (handSystem == null) return;
        handSystem.HandChanged -= DisplayHand;
        handSystem.HeldItemChanged -= DisplayItem;
    }
    static Image MakeIcon(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2, 2); rect.offsetMax = new Vector2(-2, -2);
        var image = go.GetComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true; image.enabled = false;
        return image;
    }
    void DisplayItem(HandSystem.HandSlot hand, ItemComponent item)
    {
        var icon = hand == HandSystem.HandSlot.Left ? leftItemIcon : rightItemIcon;
        if (icon == null) return;
        icon.sprite = item != null && item.Definition != null ? item.Definition.ItemIcon : null;
        icon.enabled = icon.sprite != null;
    }
    public void DisplayHand(HandSystem.HandSlot slot)
    {
        if (leftHighlight != null) leftHighlight.enabled = slot == HandSystem.HandSlot.Left;
        if (rightHighlight != null) rightHighlight.enabled = slot == HandSystem.HandSlot.Right;
    }
}
