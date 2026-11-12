using UnityEngine;

// Whether the actor should transform, given its current state and the powerup that was just
// requested. Each asset instance hard-codes the (from state, powerup) pairs it answers for, so a
// false answer means "this decision is not about the current situation".
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Decisions/Transform")]
public class TransformDecision : Decision
{
    public StateTransformMap[] map;

    public override bool Decide(StateController controller)
    {
        return MatchesMap(controller);
    }

    protected bool MatchesMap(StateController controller)
    {
        ExpeditionerStateController actor = controller as ExpeditionerStateController;
        if (actor == null || map == null) return false;
        // State asset names match the ExpeditionerState enum values one-to-one.
        ExpeditionerState current = EnumExtension.ParseEnum<ExpeditionerState>(controller.currentState.name);
        for (int i = 0; i < map.Length; i++)
            if (current == map[i].fromState && actor.currentPowerupType == map[i].powerupCollected)
                return true;
        return false;
    }
}
