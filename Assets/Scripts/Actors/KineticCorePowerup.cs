using UnityEngine;

// Armored -> Armed (equivalent of the Fire Flower: unlocks the ranged Attack event action).
[CreateAssetMenu(fileName = "KineticCorePowerup", menuName = "Lab5/Powerups/Kinetic core")]
public class KineticCorePowerup : PowerupDefinition
{
    public override void ApplyPowerup(MonoBehaviour target)
    {
        ExpeditionerStateController actor;
        if (target.TryGetComponent(out actor)) actor.SetPowerup(PowerupType.KineticCore);
    }
}
