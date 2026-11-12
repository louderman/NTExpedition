using System;
using UnityEngine;

[CreateAssetMenu(fileName = "BoolVariable", menuName = "Lab5/Variables/Bool variable", order = 1)]
public class BoolVariable : VariableBase
{
    [SerializeField] private bool defaultValue;
    [SerializeField] private bool value;
    [Tooltip("Optional shared channel raised whenever this value changes.")]
    [SerializeField] private SimpleGameEvent onValueChanged;

    public bool Value => value;
    public event Action<bool> ValueChanged;

    public void SetValue(bool newValue)
    {
        value = newValue;
        ValueChanged?.Invoke(value);
        if (onValueChanged != null) onValueChanged.Raise();
    }

    public void SetValue(BoolVariable other) => SetValue(other != null && other.Value);

    public void Toggle() => SetValue(!value);

    public void ResetToDefault() => SetValue(defaultValue);

    // Editor-time authoring helper used by the Lab 5 setup script.
    public void AuthorDefault(bool newDefault, bool applyToRuntimeValue)
    {
        defaultValue = newDefault;
        if (applyToRuntimeValue) SetValue(newDefault);
    }
}
