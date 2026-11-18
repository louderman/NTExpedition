using UnityEngine;

// Leaving a buff state ends the buff: stop its visuals, play the end clip and clear the shared
// invulnerable variable.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Actions/EndBuff")]
public class EndBuffAction : StateAction
{
    public AudioClip buffEndSound;

    public override void Act(StateController controller)
    {
        controller.StopFlicker();
        controller.PlayStateSound(buffEndSound);
        BuffStateController buffs = controller as BuffStateController;
        if (buffs != null) buffs.OnBuffEntered(0, 0f);
    }
}
