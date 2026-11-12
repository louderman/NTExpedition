using UnityEngine;

// Lab 5 pluggable state machine.
//
// Named StateAction rather than the handout's "Action" so it never collides with System.Action
// in project files that use `using System;`. Behaviour and role are identical.
public abstract class StateAction : ScriptableObject
{
    public abstract void Act(StateController controller);
}
