using System.Collections;
using UnityEngine;

// Lab 5 collectible. It is data-driven through a PowerupDefinition asset, publishes collection on a
// shared ScriptableObject event channel (no singleton manager), and is *temporary*: after its
// lifetime it blinks and disappears, raising OnCollectibleExpired.
// The class name is unchanged from the earlier lab so existing prefabs keep their script link.
[RequireComponent(typeof(Collider2D))]
public class PowerupPickup : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PowerupDefinition definition;
    [SerializeField] private ExpeditionConfig config;

    [Header("Shared event channels")]
    [SerializeField] private PowerupGameEvent onPowerupCollected;
    [SerializeField] private SimpleGameEvent onCollectibleSpawned;
    [SerializeField] private SimpleGameEvent onCollectibleExpired;

    [Header("Temporary lifetime")]
    [Tooltip("Seconds before this collectible disappears. Negative uses the config default; 0 is permanent.")]
    [SerializeField] private float lifetimeSeconds = -1f;
    [SerializeField] private float blinkSeconds = 2.5f;

    private Collider2D hitbox;
    private SpriteRenderer spriteRenderer;
    private bool ready;
    private bool consumed;
    private bool expiring;

    public bool Ready => ready && !consumed && !expiring;
    public bool Consumed => consumed;
    public PowerupDefinition Definition => definition;

    public float Lifetime => lifetimeSeconds >= 0f
        ? lifetimeSeconds
        : (config != null ? config.pickupLifetimeSeconds : 12f);

    void Awake()
    {
        hitbox = GetComponent<Collider2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        // Never collectible during emergence, from any direction.
        if (hitbox != null) hitbox.enabled = false;
        if (spriteRenderer != null && definition != null) spriteRenderer.color = definition.Tint;
    }

    IEnumerator Start()
    {
        float emergence = config != null ? config.pickupEmergenceSeconds : 0.45f;
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.down * 0.7f;
        float elapsed = 0f;
        while (elapsed < emergence)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / emergence));
            yield return null;
        }
        ready = true;
        if (hitbox != null) hitbox.enabled = true;
        if (onCollectibleSpawned != null) onCollectibleSpawned.Raise();

        float life = Lifetime;
        if (life <= 0f) yield break; // a permanent collectible never expires

        float dieAt = Time.time + life;
        while (Time.time < dieAt && !consumed)
        {
            float remaining = dieAt - Time.time;
            if (spriteRenderer != null && remaining <= blinkSeconds)
                spriteRenderer.enabled = Mathf.Repeat(remaining, 0.3f) > 0.15f;
            yield return null;
        }
        if (spriteRenderer != null) spriteRenderer.enabled = true;
        if (!consumed) Expire();
    }

    void OnTriggerEnter2D(Collider2D other) { TryCollect(other); }
    void OnTriggerStay2D(Collider2D other) { TryCollect(other); }

    // One ownership gate for all overlaps: two player colliders cannot grant the same pickup twice.
    public bool TryCollect(Collider2D other)
    {
        if (!Ready || other == null) return false;
        ExpeditionerStateController collector = other.GetComponentInParent<ExpeditionerStateController>();
        if (collector == null) return false;
        if (definition != null && !definition.CanApply(collector)) return false;

        consumed = true;
        if (hitbox != null) hitbox.enabled = false;

        // Shared channel: the collector's PowerupGameEventListener asks the powerup to apply itself.
        if (onPowerupCollected != null) onPowerupCollected.Raise(definition);
        if (definition != null && definition.CollectSound != null)
            AudioSource.PlayClipAtPoint(definition.CollectSound, transform.position);

        Destroy(gameObject);
        return true;
    }

    public void Expire()
    {
        if (consumed || expiring) return;
        expiring = true;
        if (onCollectibleExpired != null) onCollectibleExpired.Raise();
        Destroy(gameObject);
    }
}
