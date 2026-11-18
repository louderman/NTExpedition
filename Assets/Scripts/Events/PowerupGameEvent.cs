using UnityEngine;

// Powerup payload channel. The payload is a PowerupDefinition asset (an IPowerup implementation),
// which keeps the event serializable in the Inspector while preserving the handout's IPowerup idea.
[CreateAssetMenu(fileName = "PowerupGameEvent", menuName = "Lab5/Events/Powerup game event", order = 2)]
public class PowerupGameEvent : GameEvent<PowerupDefinition>
{
}
