using UnityEngine;

// Clears the requested buff once a buff state has consumed it.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Actions/ClearBuff")]
public class ClearBuffAction : StateAction
{
    public override void Act(StateController controller)
    {
        BuffStateController buffs = controller as BuffStateController;
        if (buffs != null) buffs.currentBuffType = BuffType.Default;
    }
}
