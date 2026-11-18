using UnityEngine;

// Entering a buff state starts its countdown and publishes the buff to the shared variables.
// Buff index matches the BuffState enum: 1 = overcharged, 2 = invincible, 0 = none.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Actions/StartBuff")]
public class StartBuffAction : StateAction
{
    public int buffIndex = 1;
    public float duration = 8f;

    public override void Act(StateController controller)
    {
        BuffStateController buffs = controller as BuffStateController;
        if (buffs != null) buffs.OnBuffEntered(buffIndex, duration);
    }
}
