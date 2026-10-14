using System.Collections;
using UnityEngine;

[RequireComponent(typeof(PlayerMovement), typeof(Health))]
public class PlayerPowerups : MonoBehaviour
{
    public ExpeditionConfig config;
    private float expiresAt;
    public bool IsOvercharged => expiresAt > Time.time;
    public float RemainingSeconds => Mathf.Max(0f, expiresAt - Time.time);
    public float SpeedMultiplier => IsOvercharged ? config.speedMultiplier : 1f;
    public float DamageMultiplier => IsOvercharged ? config.damageMultiplier : 1f;

    public bool Apply(PowerupPickup.Kind kind)
    {
        var health = GetComponent<Health>();
        if (health.CurrentHP <= 0f) return false;
        if (kind == PowerupPickup.Kind.Repair)
        {
            if (health.CurrentHP >= health.MaxHP) return false; // Do not waste a full-health pickup.
            health.Heal(config.repairAmount);
        }
        else expiresAt = Time.time + config.overchargeDuration; // Refresh duration, never stack base stats.
        return true;
    }
}
