using System;
using UnityEngine;

[CreateAssetMenu(fileName = "IntVariable", menuName = "Lab5/Variables/Int variable", order = 0)]
public class IntVariable : VariableBase
{
    [SerializeField] private int defaultValue;
    [SerializeField] private int value;
    [Tooltip("Optional shared channel raised whenever this value changes.")]
    [SerializeField] private IntGameEvent onValueChanged;

    public int Value => value;
    public int DefaultValue => defaultValue;
    public event Action<int> ValueChanged;

    public void SetValue(int newValue)
    {
        value = newValue;
        ValueChanged?.Invoke(value);
        if (onValueChanged != null) onValueChanged.Raise(value);
    }

    public void Add(int amount)
    {
        if (amount == 0) return;
        SetValue(value + amount);
    }

    // Reset for a new expedition. This is an explicit game reset, not a disk save.
    public void ResetToDefault() => SetValue(defaultValue);

    // Editor-time authoring helper used by the Lab 5 setup script.
    public void AuthorDefault(int newDefault, bool applyToRuntimeValue)
    {
        defaultValue = newDefault;
        if (applyToRuntimeValue) SetValue(newDefault);
    }
}
