using UnityEngine;

// No-argument channel. The handout's "Void" dummy payload is called NoData (see GameEvent.cs).
[CreateAssetMenu(fileName = "SimpleGameEvent", menuName = "Lab5/Events/Simple game event", order = 0)]
public class SimpleGameEvent : GameEvent<NoData>
{
    // Convenience overload so a no-argument event can also be raised from a UnityEvent callback.
    public void Raise() => Raise(new NoData());
}
