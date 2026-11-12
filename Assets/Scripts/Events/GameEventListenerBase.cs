using UnityEngine;

// Contract shared by every concrete listener so channels of different payload types stay generic.
public interface IGameEventListener<T>
{
    void OnEventRaised(T data);
}

// Base type only for scene lookup; intentionally holds no serialized fields.
// Unity does not serialize fields declared on a generic base class, so each concrete listener
// declares its own Event/Response fields (see the three listener variants).
public abstract class GameEventListenerBase : MonoBehaviour
{
}

// Payload used by the no-argument variants (the handout's "Void" dummy class).
[System.Serializable]
public class NoData
{
}
