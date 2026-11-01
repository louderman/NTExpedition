using UnityEngine;
using UnityEngine.Events;

// Int payload listener. The response method takes an int and receives the raised value.
public class IntGameEventListener : GameEventListenerBase, IGameEventListener<int>
{
    public IntGameEvent Event;
    public UnityEvent<int> Response;

    void OnEnable() { if (Event != null) Event.RegisterListener(this); }
    void OnDisable() { if (Event != null) Event.UnregisterListener(this); }

    public void OnEventRaised(int data) => Response.Invoke(data);
}
