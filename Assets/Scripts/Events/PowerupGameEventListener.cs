using UnityEngine;
using UnityEngine.Events;

// Powerup payload listener. The response method takes a PowerupDefinition.
public class PowerupGameEventListener : GameEventListenerBase, IGameEventListener<PowerupDefinition>
{
    public PowerupGameEvent Event;
    public UnityEvent<PowerupDefinition> Response;

    void OnEnable() { if (Event != null) Event.RegisterListener(this); }
    void OnDisable() { if (Event != null) Event.UnregisterListener(this); }

    public void OnEventRaised(PowerupDefinition data) => Response.Invoke(data);
}
