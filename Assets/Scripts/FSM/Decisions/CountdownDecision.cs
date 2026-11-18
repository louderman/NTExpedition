using UnityEngine;

// True once the controller has spent `buffDuration` seconds in its current state.
// The handout uses this for both the actor's temporary invincibility and the buff countdowns.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Decisions/Countdown")]
public class CountdownDecision : Decision
{
    public float buffDuration = 2f;

    public override bool Decide(StateController controller)
    {
        return controller.CheckIfCountDownElapsed(buffDuration);
    }
}
