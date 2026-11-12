using UnityEngine;

// Shield/star equivalent: temporary invincibility buff, again owned by the buff state machine.
[CreateAssetMenu(fileName = "ShieldPowerup", menuName = "Lab5/Powerups/Shield")]
public class ShieldPowerup : PowerupDefinition
{
    public override void ApplyPowerup(MonoBehaviour target)
    {
        BuffStateController buffs;
        if (target.TryGetComponent(out buffs)) buffs.ApplyBuff(BuffType.Invincible);
    }
}
