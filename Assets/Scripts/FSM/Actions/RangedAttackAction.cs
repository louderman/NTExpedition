using UnityEngine;

// Cast when the Armed state receives the Attack event action (handout's FireAttackAction).
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Actions/RangedAttack")]
public class RangedAttackAction : StateAction
{
    public int maxPrefabInScene = 3;
    public float degree = 45f;

    // A ScriptableObject asset cannot reference a scene component, so facing is read from a
    // BoolVariable that PlayerMovement keeps up to date.
    public BoolVariable playerFaceRight;

    public override void Act(StateController controller)
    {
        ExpeditionerStateController actor = controller as ExpeditionerStateController;
        if (actor == null) return;
        actor.SpawnRangedAttack(degree, maxPrefabInScene, playerFaceRight);
    }
}
