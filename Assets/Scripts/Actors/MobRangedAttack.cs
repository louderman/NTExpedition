using UnityEngine;

[RequireComponent(typeof(MobMovement), typeof(Health), typeof(Rigidbody2D))]
public class MobRangedAttack : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float stopDistance = 4f;
    [SerializeField, Min(0.1f)] float resumeDistance = 5f;
    [SerializeField, Min(0.1f)] float shootingRange = 8f;
    [SerializeField, Min(0.1f)] float shotInterval = 1.5f;
    [SerializeField, Min(0.05f)] float windupDuration = 0.3f;
    [SerializeField, Min(1)] int damage = 15;
    [SerializeField, Min(0.1f)] float projectileSpeed = 8f;
    [SerializeField] Sprite projectileSprite;
    [SerializeField] SpriteRenderer mobRenderer;
    [SerializeField] IntGameEvent onPlayerDamaged;

    MobMovement movement;
    Health health;
    Rigidbody2D body;
    Collider2D bodyCollider;
    Color restingColor;
    float nextWindupTime;
    float windupStarted = -1f;

    void Awake()
    {
        movement = GetComponent<MobMovement>();
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        restingColor = mobRenderer.color;
    }

    void OnEnable()
    {
        health.Died += StopAttacking;
        movement.Interrupted += CancelShot;
        CancelShot();
    }

    void OnDisable()
    {
        health.Died -= StopAttacking;
        movement.Interrupted -= CancelShot;
        CancelShot();
    }

    void OnValidate()
    {
        resumeDistance = Mathf.Max(stopDistance + 0.1f, resumeDistance);
        shootingRange = Mathf.Max(resumeDistance, shootingRange);
        shotInterval = Mathf.Max(windupDuration, shotInterval);
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        if (!movement.CanAct || !movement.IsAggroed || movement.Target == null ||
            health.CurrentHP <= 0f || onPlayerDamaged == null || projectileSprite == null)
        {
            CancelShot();
            return;
        }

        var targetHealth = movement.Target.GetComponent<Health>();
        Vector2 origin = bodyCollider.bounds.center;
        Vector2 target = movement.Target.position;
        float distance = Vector2.Distance(origin, target);
        if ((targetHealth != null && targetHealth.CurrentHP <= 0f) || distance > shootingRange ||
            !KineticProjectile.HasClearShot(origin, target, body))
        {
            CancelShot();
            return;
        }

        movement.HoldPosition = distance <= (movement.HoldPosition ? resumeDistance : stopDistance);
        movement.FaceTarget();
        if (Time.time < nextWindupTime) return;
        if (windupStarted < 0f) windupStarted = Time.time;

        float progress = (Time.time - windupStarted) / windupDuration;
        mobRenderer.color = Color.Lerp(restingColor, new Color(1f, 0.4f, 0.1f), progress);
        if (progress < 1f) return;

        KineticProjectile.Launch(origin, target - origin, body, damage, projectileSpeed,
            shootingRange, projectileSprite, onPlayerDamaged);
        windupStarted = -1f;
        mobRenderer.color = restingColor;
        nextWindupTime = Time.time + shotInterval - windupDuration;
    }

    void CancelShot()
    {
        movement.HoldPosition = false;
        windupStarted = -1f;
        nextWindupTime = Time.time + shotInterval - windupDuration;
        mobRenderer.color = restingColor;
    }

    void StopAttacking() => enabled = false;
}
