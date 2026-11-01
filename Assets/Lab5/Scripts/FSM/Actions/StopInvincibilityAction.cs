using UnityEngine;

// Exit action for an invincible state.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Actions/StopInvincibility")]
public class StopInvincibilityAction : StateAction
{
    public override void Act(StateController controller)
    {
        controller.StopFlicker();
    }
}
