using UnityEngine;

// Repair supplies: heals instead of changing tier, and refuses to be consumed at full health.
[CreateAssetMenu(fileName = "RepairPowerup", menuName = "Lab5/Powerups/Repair")]
public class RepairPowerup : PowerupDefinition
{
    [SerializeField] private float repairAmount = 40f;

    public float RepairAmount => repairAmount;

    public override bool CanApply(MonoBehaviour target)
    {
        Health health;
        return target != null && target.TryGetComponent(out health)
            && health.CurrentHP > 0f && health.CurrentHP < health.MaxHP;
    }

    public override void ApplyPowerup(MonoBehaviour target)
    {
        Health health;
        if (target.TryGetComponent(out health)) health.Heal(repairAmount);
    }

    public void AuthorAmount(float amount) => repairAmount = amount;
}
