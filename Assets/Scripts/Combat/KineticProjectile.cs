using UnityEngine;

public class KineticProjectile : MonoBehaviour
{
    Rigidbody2D owner;
    Vector2 direction;
    float damage;
    float speed;
    float remainingRange;
    IntGameEvent onPlayerDamaged;

    // Enemy shots supply the player damage channel; ordinary weapon shots damage Health directly.
    public static void Launch(Vector2 origin, Vector2 direction, Rigidbody2D ownerBody,
        float shotDamage, float shotSpeed, float shotRange, Sprite projectileSprite,
        IntGameEvent onPlayerDamaged = null)
    {
        if (projectileSprite == null)
        {
            Debug.LogError("Kinetic projectile sprite is not configured.");
            return;
        }
        var bolt = new GameObject("Kinetic bolt");
        bolt.transform.position = origin;
        // Space Station 14's world angle is south-based (Angle.ToWorldVec: 0 = south) while Unity's
        // Atan2 is east-based, so +90 degrees converts travel direction into Frontier's rotation.
        // BulletKinetic uses noRot:false and its one-direction chronobolt state resolves to the
        // South RSI row, which Frontier draws with no extra direction matrix, so the authored art
        // (pointing south) ends up pointing along the shot exactly as it does upstream.
        bolt.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f);
        var renderer = bolt.AddComponent<SpriteRenderer>();
        renderer.sprite = projectileSprite;
        renderer.sortingOrder = 18;
        var projectile = bolt.AddComponent<KineticProjectile>();
        projectile.Initialize(direction, ownerBody, shotDamage, shotSpeed, shotRange);
        projectile.onPlayerDamaged = onPlayerDamaged;
    }

    void Initialize(Vector2 shotDirection, Rigidbody2D ownerBody, float shotDamage, float shotSpeed, float shotRange)
    {
        direction = shotDirection.normalized;
        owner = ownerBody;
        damage = shotDamage;
        speed = shotSpeed;
        remainingRange = shotRange;
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        float distance = Mathf.Min(speed * Time.deltaTime, remainingRange);
        if (distance <= 0f) { Destroy(gameObject); return; }
        Vector2 start = transform.position;
        var hit = FirstHit(start, direction, distance, owner, onPlayerDamaged != null);
        if (hit.collider != null)
        {
            var health = hit.collider.attachedRigidbody != null
                ? hit.collider.attachedRigidbody.GetComponent<Health>()
                : hit.collider.GetComponentInParent<Health>();
            if (onPlayerDamaged != null)
            {
                if (hit.collider.GetComponentInParent<ExpeditionerStateController>() != null)
                    onPlayerDamaged.Raise(Mathf.RoundToInt(damage));
            }
            else if (health != null && health.CurrentHP > 0f) health.TakeDamage(damage);
            transform.position = hit.point;
            Destroy(gameObject);
            return;
        }
        transform.position = start + direction * distance;
        remainingRange -= distance;
        if (remainingRange <= 0f) Destroy(gameObject);
    }

    // Enemy sight and projectile travel use the same blockers, so mobs cannot see through cover.
    public static bool HasClearShot(Vector2 origin, Vector2 target, Rigidbody2D owner)
    {
        Vector2 delta = target - origin;
        var hit = FirstHit(origin, delta.normalized, delta.magnitude, owner, true);
        return hit.collider == null ||
            hit.collider.GetComponentInParent<ExpeditionerStateController>() != null;
    }

    static RaycastHit2D FirstHit(Vector2 origin, Vector2 direction, float distance,
        Rigidbody2D owner, bool playerOnly)
    {
        foreach (var hit in Physics2D.RaycastAll(origin, direction, distance))
        {
            if (hit.collider == null || (owner != null && hit.collider.attachedRigidbody == owner)) continue;
            if (playerOnly && hit.collider.GetComponentInParent<MobMovement>() != null) continue;
            if (hit.collider.isTrigger && hit.collider.GetComponentInParent<Health>() == null) continue;
            return hit;
        }
        return default;
    }
}
