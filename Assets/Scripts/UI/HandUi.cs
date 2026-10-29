using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HandUI : MonoBehaviour
{
    [SerializeField] private Image leftHighlight;
    [SerializeField] private Image rightHighlight;
    [SerializeField] private Image leftSlot;
    [SerializeField] private Image rightSlot;
    [SerializeField] private Image backpackSlot;
    [SerializeField] private HandSystem handSystem;
    Image leftItemIcon, rightItemIcon;
    public Image LeftItemIcon => leftItemIcon;
    public Image RightItemIcon => rightItemIcon;
    public Image LeftSlot => leftSlot;
    public Image RightSlot => rightSlot;
    public Image BackpackSlot => backpackSlot;

    void OnEnable()
    {
        if (handSystem == null || leftHighlight == null || rightHighlight == null) return;
        if (leftItemIcon == null) leftItemIcon = MakeIcon(leftHighlight.transform, "Left held item");
        if (rightItemIcon == null) rightItemIcon = MakeIcon(rightHighlight.transform, "Right held item");
        leftHighlight.raycastTarget = false;
        rightHighlight.raycastTarget = false;
        BindSlot(leftSlot, HandSystem.HandSlot.Left);
        BindSlot(rightSlot, HandSystem.HandSlot.Right);
        handSystem.HandChanged += DisplayHand;
        handSystem.HeldItemChanged += DisplayItem;
        DisplayHand(handSystem.CurrentHandSlot);
        DisplayItem(HandSystem.HandSlot.Left, handSystem.HeldItem(HandSystem.HandSlot.Left));
        DisplayItem(HandSystem.HandSlot.Right, handSystem.HeldItem(HandSystem.HandSlot.Right));
    }
    void BindSlot(Image slot, HandSystem.HandSlot hand)
    {
        if (slot == null) return;
        slot.raycastTarget = true;
        var target = slot.GetComponent<HandSlotClickTarget>();
        if (target == null) target = slot.gameObject.AddComponent<HandSlotClickTarget>();
        target.Configure(handSystem, hand);
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

public class HandSlotClickTarget : MonoBehaviour, IPointerClickHandler
{
    HandSystem handSystem;
    HandSystem.HandSlot hand;
    public void Configure(HandSystem system, HandSystem.HandSlot slot) { handSystem = system; hand = slot; }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && handSystem != null)
            handSystem.TryMoveActiveItemTo(hand);
    }
}
