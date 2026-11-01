using UnityEngine;

// Minimal per-item ranged configuration. Ammo is intentionally unlimited: firing is gated only by cooldown.
public class RangedWeapon : MonoBehaviour
{
    [SerializeField, Min(0f)] float damage = 5f;
    [SerializeField, Min(0.05f)] float fireCooldown = .65f;
    [SerializeField, Min(.1f)] float range = 8f;
    [SerializeField, Min(.1f)] float projectileSpeed = 12f;
    [SerializeField] Sprite glowSprite;
    [SerializeField] Sprite projectileSprite;
    float nextFireTime;

    public float Damage => damage;
    public float FireCooldown => fireCooldown;
    public float Range => range;
    public float ProjectileSpeed => projectileSpeed;
    public Sprite GlowSprite => glowSprite;
    public Sprite ProjectileSprite => projectileSprite;
    public int ShotsFired { get; private set; }
    public Vector2 LastShotDirection { get; private set; }

    public bool TryFire(Vector2 origin, Vector2 direction, Rigidbody2D owner, float damageMultiplier)
    {
        if (direction.sqrMagnitude < .0001f || Time.time < nextFireTime) return false;
        direction.Normalize();
        nextFireTime = Time.time + fireCooldown;
        LastShotDirection = direction;
        ShotsFired++;
        KineticProjectile.Launch(origin, direction, owner, damage * damageMultiplier, projectileSpeed, range,
            projectileSprite);
        return true;
    }
}
