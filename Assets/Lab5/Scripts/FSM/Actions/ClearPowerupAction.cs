using UnityEngine;

// Clears the requested actor powerup once a state has consumed it.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Actions/ClearPowerup")]
public class ClearPowerupAction : StateAction
{
    public override void Act(StateController controller)
    {
        ExpeditionerStateController actor = controller as ExpeditionerStateController;
        if (actor != null) actor.currentPowerupType = PowerupType.Default;
    }
}
