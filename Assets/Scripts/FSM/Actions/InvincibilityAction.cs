using UnityEngine;

// Invincibility (Super Star equivalent): play a clip and flicker the renderers until the state ends.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Actions/SetupInvincibility")]
public class InvincibilityAction : StateAction
{
    public AudioClip invincibilityStart;
    public float flickerInterval = 0.12f;

    public override void Act(StateController controller)
    {
        controller.PlayStateSound(invincibilityStart);
        controller.StartFlicker(flickerInterval);
    }
}
