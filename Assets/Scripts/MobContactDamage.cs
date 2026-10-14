using UnityEngine;

// A contact is damage, not a bypass around Health.Died and the game-over observers.
public class MobContactDamage : MonoBehaviour
{
    public float damage = 10f;
    public float interval = 1f;
    private float nextHit;
    void OnCollisionStay2D(Collision2D collision)
    {
        if (Time.timeScale == 0f || Time.time < nextHit) return;
        var movement = GetComponent<MobMovement>();
        var ownHealth = GetComponent<Health>();
        if (movement == null || movement.isStunned || (ownHealth != null && ownHealth.CurrentHP <= 0f)) return;
        var player = collision.gameObject.GetComponent<PlayerPowerups>();
        if (player == null) return;
        nextHit = Time.time + interval;
        player.GetComponent<Health>().TakeDamage(damage);
    }
}
