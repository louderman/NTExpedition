using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HandUI : MonoBehaviour
{
    // Sentence-case label + icon shown on the hand a world fixture (the storage crate) is occupying while it
    // is dragged. Side-specific because each pair is parented to its own hand slot. The icon is the
    // fixture's OWN authored sprite (StorageCrate reports it through IDragTarget.DragIcon), so the occupied
    // hand is recognisable at a glance instead of only readable as text.
    const string OccupiedLabel = "Dragging crate";
    const float OccupiedIconSize = 16f;
    const float OccupiedIconGap = 3f;
    const float OccupiedLabelWidth = 110f;
    const float OccupiedLabelHeight = 16f;
    const float OccupiedLabelShiftX = 8f;   // room for the icon on the label's left

    [SerializeField] private Image leftHighlight;
    [SerializeField] private Image rightHighlight;
    [SerializeField] private Image leftSlot;
    [SerializeField] private Image rightSlot;
    [SerializeField] private Image backpackSlot;
    [SerializeField] private HandSystem handSystem;
    [SerializeField] private TMP_FontAsset occupiedLabelFont;
    Image leftItemIcon, rightItemIcon;
    TextMeshProUGUI leftOccupied, rightOccupied;
    Image leftOccupiedIcon, rightOccupiedIcon;
    public Image LeftItemIcon => leftItemIcon;
    public Image RightItemIcon => rightItemIcon;
    public Image LeftSlot => leftSlot;
    public Image RightSlot => rightSlot;
    public Image BackpackSlot => backpackSlot;
    public Image LeftOccupiedIcon => leftOccupiedIcon;
    public Image RightOccupiedIcon => rightOccupiedIcon;

    void OnEnable()
    {
        if (handSystem == null || leftHighlight == null || rightHighlight == null) return;
        if (leftItemIcon == null) leftItemIcon = MakeIcon(leftHighlight.transform, "Left held item");
        if (rightItemIcon == null) rightItemIcon = MakeIcon(rightHighlight.transform, "Right held item");
        if (leftOccupied == null) leftOccupied = MakeOccupiedLabel(leftSlot != null ? leftSlot.transform : leftHighlight.transform, "Left hand occupied");
        if (rightOccupied == null) rightOccupied = MakeOccupiedLabel(rightSlot != null ? rightSlot.transform : rightHighlight.transform, "Right hand occupied");
        if (leftOccupiedIcon == null) leftOccupiedIcon = MakeOccupiedIcon(leftSlot != null ? leftSlot.transform : leftHighlight.transform, "Left hand occupied icon");
        if (rightOccupiedIcon == null) rightOccupiedIcon = MakeOccupiedIcon(rightSlot != null ? rightSlot.transform : rightHighlight.transform, "Right hand occupied icon");
        leftHighlight.raycastTarget = false;
        rightHighlight.raycastTarget = false;
        BindSlot(leftSlot, HandSystem.HandSlot.Left);
        BindSlot(rightSlot, HandSystem.HandSlot.Right);
        handSystem.HandChanged += DisplayHand;
        handSystem.HeldItemChanged += DisplayItem;
        handSystem.DragHandReservedChanged += DisplayOccupied;
        DisplayHand(handSystem.CurrentHandSlot);
        DisplayItem(HandSystem.HandSlot.Left, handSystem.HeldItem(HandSystem.HandSlot.Left));
        DisplayItem(HandSystem.HandSlot.Right, handSystem.HeldItem(HandSystem.HandSlot.Right));
        DisplayOccupied(HandSystem.HandSlot.Left, handSystem.IsDragHandReserved(HandSystem.HandSlot.Left));
        DisplayOccupied(HandSystem.HandSlot.Right, handSystem.IsDragHandReserved(HandSystem.HandSlot.Right));
    }

    /// <summary>True while the occupied label for that hand is on screen (used by the drag regression observer).</summary>
    public bool IsOccupiedShown(HandSystem.HandSlot hand)
    {
        var label = hand == HandSystem.HandSlot.Left ? leftOccupied : rightOccupied;
        return label != null && label.enabled;
    }

    /// <summary>True while the occupied crate icon for that hand is on screen (used by the drag observer).</summary>
    public bool IsOccupiedIconShown(HandSystem.HandSlot hand)
    {
        var icon = hand == HandSystem.HandSlot.Left ? leftOccupiedIcon : rightOccupiedIcon;
        return icon != null && icon.enabled && icon.sprite != null;
    }

    /// <summary>The sprite a hand's occupied icon is showing, or null when it is not showing one.</summary>
    public Sprite OccupiedIconSprite(HandSystem.HandSlot hand)
    {
        var icon = hand == HandSystem.HandSlot.Left ? leftOccupiedIcon : rightOccupiedIcon;
        return icon != null && icon.enabled ? icon.sprite : null;
    }

    public string OccupiedText => OccupiedLabel;
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
        handSystem.DragHandReservedChanged -= DisplayOccupied;
    }
    // A "Dragging crate" label parented to the hand's own slot, parked just above it. Created at runtime like
    // the item icons, so no scene edit was needed for the HUD to gain the occupied state. Only ever one hand
    // is booked at a time, so the two labels never show together.
    TextMeshProUGUI MakeOccupiedLabel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 1f); rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        // Shifted right of centre so the crate icon can sit on the label's left: the icon + gap + label row
        // is what ends up centred on the slot.
        rect.anchoredPosition = new Vector2(OccupiedLabelShiftX, 4f);
        rect.sizeDelta = new Vector2(OccupiedLabelWidth, OccupiedLabelHeight);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (occupiedLabelFont != null) text.font = occupiedLabelFont;
        text.text = OccupiedLabel;
        text.fontSize = 10f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.89f, 0.56f, 0.01f);   // the project's accent orange
        text.raycastTarget = false;
        text.enabled = false;
        return text;
    }
    // The icon half of the occupied state: the fixture's own authored sprite, parked on the label's left just
    // above the hand slot, vertically centred with the label. Runtime-created like the label, so the HUD gains
    // it with no scene edit, and it is a SEPARATE image from the held-item icon - the slot itself keeps
    // rendering an equipped item normally, so the drag indicator can never be mistaken for a carried item.
    Image MakeOccupiedIcon(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 1f); rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(OccupiedIconSize, OccupiedIconSize);
        rect.anchoredPosition = new Vector2(
            OccupiedLabelShiftX - OccupiedLabelWidth * .5f - OccupiedIconGap - OccupiedIconSize * .5f,
            OccupiedLabelHeight * .5f + 4f);
        var image = go.GetComponent<Image>();
        image.raycastTarget = false; image.preserveAspect = true; image.enabled = false;
        return image;
    }
    void DisplayOccupied(HandSystem.HandSlot hand, bool reserved)
    {
        var label = hand == HandSystem.HandSlot.Left ? leftOccupied : rightOccupied;
        if (label != null) label.enabled = reserved;

        var icon = hand == HandSystem.HandSlot.Left ? leftOccupiedIcon : rightOccupiedIcon;
        if (icon == null) return;
        // Only the booked fixture's own art is ever shown, and only while this hand is booked: a booking with
        // no art - or no booking at all - stays text-only exactly as before.
        var sprite = reserved && handSystem != null ? handSystem.DragIconFor(hand) : null;
        if (sprite != null) icon.sprite = sprite;
        icon.enabled = sprite != null;
    }
    /// <summary>Screen-space rect of a hand's occupied label, so a run can prove where the HUD put it.</summary>
    public Rect OccupiedLabelScreenRect(HandSystem.HandSlot hand)
    {
        var label = hand == HandSystem.HandSlot.Left ? leftOccupied : rightOccupied;
        if (label == null) return new Rect();
        var world = label.rectTransform.TransformPoint(label.rectTransform.rect.center);
        var screen = RectTransformUtility.WorldToScreenPoint(null, world);
        var size = label.rectTransform.rect.size;
        return new Rect(screen.x - size.x * .5f, screen.y - size.y * .5f, size.x, size.y);
    }
    /// <summary>Screen-space rect of a hand's occupied icon, mirroring OccupiedLabelScreenRect.</summary>
    public Rect OccupiedIconScreenRect(HandSystem.HandSlot hand)
    {
        var icon = hand == HandSystem.HandSlot.Left ? leftOccupiedIcon : rightOccupiedIcon;
        if (icon == null) return new Rect();
        var world = icon.rectTransform.TransformPoint(icon.rectTransform.rect.center);
        var screen = RectTransformUtility.WorldToScreenPoint(null, world);
        var size = icon.rectTransform.rect.size;
        return new Rect(screen.x - size.x * .5f, screen.y - size.y * .5f, size.x, size.y);
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
