using UnityEngine;
using UnityEngine.Events;

// Attach this to the GameObject that owns the callback, link the SimpleGameEvent asset and the
// public method in the Inspector. Multiple listeners may live on the same GameObject.
public class SimpleGameEventListener : GameEventListenerBase, IGameEventListener<NoData>
{
    public SimpleGameEvent Event;
    public UnityEvent Response;

    void OnEnable() { if (Event != null) Event.RegisterListener(this); }
    void OnDisable() { if (Event != null) Event.UnregisterListener(this); }

    public void OnEventRaised(NoData data) => Response.Invoke();
}
