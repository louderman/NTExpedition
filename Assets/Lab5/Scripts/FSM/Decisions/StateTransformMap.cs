using UnityEngine;

// (from state, powerup) pair consumed by TransformDecision.
[System.Serializable]
public struct StateTransformMap
{
    public ExpeditionerState fromState;
    public PowerupType powerupCollected;
}
