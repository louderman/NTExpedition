using UnityEngine;

// Equivalent of the handout's SetupAnimatorAction: entering a state changes how the actor looks.
// NTExpedition draws the crew member with SpriteRenderers, so a "tier" is an index plus tints.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Actions/SetupTier")]
public class SetupTierAction : StateAction
{
    public int tier;

    public override void Act(StateController controller)
    {
        ExpeditionerStateController actor = controller as ExpeditionerStateController;
        if (actor != null) actor.SetActiveTier(tier);
    }
}
