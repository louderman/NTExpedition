using TMPro;
using UnityEngine;

// Renders a ScriptableObject variable's value into a TMP text.
// Intended response target for an IntGameEventListener (inspector-wired) so the HUD is updated by
// the shared event channel rather than by polling a manager singleton.
public class IntVariableText : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private IntVariable source;
    [SerializeField] private string prefix = "";

    public void SetText(int value)
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (text == null) return;
        text.text = string.IsNullOrEmpty(prefix) ? value.ToString() : prefix + value;
    }

    void Start()
    {
        // Initial render for a freshly loaded scene (the event only fires on later changes).
        if (source != null) SetText(source.Value);
    }
}
