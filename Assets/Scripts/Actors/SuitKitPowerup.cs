using UnityEngine;

// Base -> Armored (equivalent of the Magic Mushroom: one extra hit absorbed).
[CreateAssetMenu(fileName = "SuitKitPowerup", menuName = "Lab5/Powerups/Suit kit")]
public class SuitKitPowerup : PowerupDefinition
{
    public override void ApplyPowerup(MonoBehaviour target)
    {
        ExpeditionerStateController actor;
        if (target.TryGetComponent(out actor)) actor.SetPowerup(PowerupType.SuitKit);
    }
}
