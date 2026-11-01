using UnityEngine;

// A damage transition that only fires once the actor's health is actually exhausted.
// Damage in a higher tier still steps a tier down (TransformDecision); damage in the base tier is
// only fatal at 0 HP.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Decisions/FatalDamage")]
public class FatalDamageDecision : TransformDecision
{
    public override bool Decide(StateController controller)
    {
        if (!MatchesMap(controller)) return false;
        Health health = controller.GetComponent<Health>();
        return health != null && health.CurrentHP <= 0f;
    }
}
