using System.Collections.Generic;
using UnityEngine;

// Lab 5 (Teens): ScriptableObject game architecture.
//
// A GameEvent<T> is a shared event *channel asset*. Listeners are stored inside the asset itself,
// not on a scene object, so any scene can raise an event and any scene can subscribe to it.
// GameEventListener components register in OnEnable and unregister in OnDisable.
//
// Note: the handout's dummy "Void" type is called NoData here, because a global class named Void
// is ambiguous with System.Void in files that use `using System;`.
public class GameEvent<T> : ScriptableObject
{
    private readonly List<IGameEventListener<T>> eventListeners = new List<IGameEventListener<T>>();

    // Used by the Lab 5 verification harness to prove a channel has real scene subscribers.
    public int ListenerCount => eventListeners.Count;

    public void Raise(T data)
    {
        for (int i = eventListeners.Count - 1; i >= 0; i--)
        {
            IGameEventListener<T> listener = eventListeners[i];
            // Interface references do not get Unity's "destroyed object == null" overload,
            // so destroyed listeners are detected through an explicit UnityEngine.Object check.
            if (listener is Object unityObject && unityObject == null)
            {
                eventListeners.RemoveAt(i);
                continue;
            }
            listener.OnEventRaised(data);
        }
    }

    public void RegisterListener(IGameEventListener<T> listener)
    {
        if (listener == null || eventListeners.Contains(listener)) return;
        eventListeners.Add(listener);
    }

    public void UnregisterListener(IGameEventListener<T> listener)
    {
        if (listener == null) return;
        eventListeners.Remove(listener);
    }
}
