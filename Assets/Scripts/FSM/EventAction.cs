using UnityEngine;

// Which input/event channel an EventAction responds to. Only matching types are cast by the state.
public enum ActionType
{
    Default = -1,
    Attack = 0,
    Trigger = 1
}

[System.Serializable]
public struct EventAction
{
    public StateAction action;
    public ActionType type;
}
