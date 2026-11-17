using UnityEngine;

// Minimal per-item ranged configuration. Ammo is intentionally unlimited: firing is gated only by cooldown.
public class RangedWeapon : MonoBehaviour
{
    public enum HeldDirection { South, North, East, West }

    [SerializeField, Min(0f)] float damage = 5f;
    [SerializeField, Min(0.05f)] float fireCooldown = .65f;
    [SerializeField, Min(.1f)] float range = 8f;
    [SerializeField, Min(.1f)] float projectileSpeed = 12f;
    [SerializeField] Sprite glowSprite;
    [SerializeField] Sprite projectileSprite;
    [SerializeField] bool supportsTwoHandedUse;
    [SerializeField, Range(0f, 45f)] float oneHandedSpreadDegrees;
    [SerializeField, Range(0f, 45f)] float wieldedSpreadDegrees;
    [SerializeField] Sprite[] oneHandedLeftSprites;
    [SerializeField] Sprite[] oneHandedRightSprites;
    [SerializeField] Sprite[] wieldedLeftSprites;
    [SerializeField] Sprite[] wieldedRightSprites;
    [SerializeField] Sprite muzzleFlashSprite;
    [SerializeField] AudioClip fireSound;
    float nextFireTime;

    public float Damage => damage;
    public float FireCooldown => fireCooldown;
    public float Range => range;
    public float ProjectileSpeed => projectileSpeed;
    public Sprite GlowSprite => glowSprite;
    public Sprite ProjectileSprite => projectileSprite;
    public bool SupportsTwoHandedUse => supportsTwoHandedUse;
    public float OneHandedSpreadDegrees => oneHandedSpreadDegrees;
    public float WieldedSpreadDegrees => wieldedSpreadDegrees;
    public Sprite MuzzleFlashSprite => muzzleFlashSprite;
    public AudioClip FireSound => fireSound;
    public int ShotsFired { get; private set; }
    public Vector2 LastShotDirection { get; private set; }

    public Sprite HeldSprite(bool wielded, HandSystem.HandSlot hand, HeldDirection direction)
    {
        Sprite[] sprites = wielded
            ? hand == HandSystem.HandSlot.Left ? wieldedLeftSprites : wieldedRightSprites
            : hand == HandSystem.HandSlot.Left ? oneHandedLeftSprites : oneHandedRightSprites;
        int index = (int)direction;
        return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }

    public bool HasHeldSprites(bool wielded, HandSystem.HandSlot hand)
    {
        var sprites = wielded
            ? hand == HandSystem.HandSlot.Left ? wieldedLeftSprites : wieldedRightSprites
            : hand == HandSystem.HandSlot.Left ? oneHandedLeftSprites : oneHandedRightSprites;
        return sprites != null && sprites.Length == 4
            && sprites[0] != null && sprites[1] != null && sprites[2] != null && sprites[3] != null;
    }

    public Vector2 ApplySpread(Vector2 direction, bool wielded)
    {
        if (direction.sqrMagnitude < .0001f) return Vector2.zero;
        direction.Normalize();
        float spread = wielded ? wieldedSpreadDegrees : oneHandedSpreadDegrees;
        return Quaternion.Euler(0f, 0f, Random.Range(-spread, spread)) * direction;
    }

    public bool TryFire(Vector2 origin, Vector2 direction, Rigidbody2D owner, float damageMultiplier)
    {
        return TryFire(origin, direction, owner, damageMultiplier, false);
    }

    public bool TryFire(Vector2 origin, Vector2 direction, Rigidbody2D owner, float damageMultiplier, bool wielded)
    {
        if (direction.sqrMagnitude < .0001f || Time.time < nextFireTime) return false;
        direction = ApplySpread(direction, wielded);
        nextFireTime = Time.time + fireCooldown;
        LastShotDirection = direction;
        ShotsFired++;
        // Firing feedback, ported from Frontier's PKA: a 0.4 s muzzle flash at the muzzle plus the
        // kinetic_accel gunshot. Both are one-shot world objects because the held item's own
        // GameObject is inactive while carried.
        MuzzleFlashEffect.Spawn(muzzleFlashSprite, origin, direction);
        FiringSoundEffect.Play(fireSound, origin);
        KineticProjectile.Launch(origin, direction, owner, damage * damageMultiplier, projectileSpeed, range,
            projectileSprite);
        return true;
    }
}
