using UnityEngine;

// Setup action for the Downed state: zero the health, stop visuals, raise OnPlayerDied.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Actions/EnterDowned")]
public class EnterDownedAction : StateAction
{
    public override void Act(StateController controller)
    {
        ExpeditionerStateController actor = controller as ExpeditionerStateController;
        if (actor != null) actor.EnterDowned();
    }
}
