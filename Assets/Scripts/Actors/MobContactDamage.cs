using UnityEngine;

// Mob contact damage. The mob no longer reaches into the player's components: it raises the shared
// OnPlayerDamaged channel with the damage amount, and whatever listens (the player's actor state
// machine) decides what that damage means.
public class MobContactDamage : MonoBehaviour
{
    [SerializeField] private int damage = 10;
    [SerializeField] private float interval = 1f;
    [SerializeField] private IntGameEvent onPlayerDamaged;

    private float nextHit;

    void OnCollisionStay2D(Collision2D collision)
    {
        if (Time.timeScale == 0f || Time.time < nextHit) return;
        var movement = GetComponent<MobMovement>();
        var ownHealth = GetComponent<Health>();
        if (movement == null || movement.isStunned || (ownHealth != null && ownHealth.CurrentHP <= 0f)) return;
        if (collision.gameObject.GetComponentInParent<ExpeditionerStateController>() == null) return;
        nextHit = Time.time + interval;
        if (onPlayerDamaged != null) onPlayerDamaged.Raise(damage);
    }
}
