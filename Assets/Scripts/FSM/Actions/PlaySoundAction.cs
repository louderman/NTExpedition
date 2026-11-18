using UnityEngine;

// Every state change plays a clip: the checkoff's "each state change has a sound effect" rule.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Actions/PlaySound")]
public class PlaySoundAction : StateAction
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;

    public override void Act(StateController controller)
    {
        controller.PlayStateSound(clip, volume);
    }
}
