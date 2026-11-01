using UnityEngine;

// Single int payload channel (score, damage amount, tier index, buff index).
[CreateAssetMenu(fileName = "IntGameEvent", menuName = "Lab5/Events/Int game event", order = 1)]
public class IntGameEvent : GameEvent<int>
{
}
