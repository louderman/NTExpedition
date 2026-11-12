using UnityEngine;

// Suit overcharge: temporary speed/damage buff, owned by the separate buff state machine.
[CreateAssetMenu(fileName = "SuitOverchargePowerup", menuName = "Lab5/Powerups/Suit overcharge")]
public class SuitOverchargePowerup : PowerupDefinition
{
    public override void ApplyPowerup(MonoBehaviour target)
    {
        BuffStateController buffs;
        if (target.TryGetComponent(out buffs)) buffs.ApplyBuff(BuffType.Overcharged);
    }
}
