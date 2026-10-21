using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class PowerupPickup : MonoBehaviour
{
    public enum Kind { Repair, Overcharge }
    public Kind kind;
    public ExpeditionConfig config;
    private Collider2D hitbox;
    private bool ready;
    private bool consumed;
    public bool Ready => ready && !consumed;

    void Awake()
    {
        hitbox = GetComponent<Collider2D>();
        hitbox.enabled = false; // Never collectible during emergence, from ANY direction.
    }

    IEnumerator Start()
    {
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.down * 0.7f;
        float elapsed = 0f;
        while (elapsed < config.pickupEmergenceSeconds)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / config.pickupEmergenceSeconds));
            yield return null;
        }
        ready = true;
        hitbox.enabled = true;
    }

    void OnTriggerEnter2D(Collider2D other) { TryCollect(other); }
    void OnTriggerStay2D(Collider2D other) { TryCollect(other); }

    // One ownership gate for all overlaps: two player colliders cannot grant it twice.
    public bool TryCollect(Collider2D other)
    {
        if (!Ready || other == null) return false;
        var buffs = other.GetComponentInParent<PlayerPowerups>();
        if (buffs == null || !buffs.Apply(kind)) return false;
        consumed = true;
        hitbox.enabled = false;
        var flow = FindAnyObjectByType<ExpeditionFlow>();
        if (flow != null) flow.PlayEffect(config.pickupSound);
        Destroy(gameObject);
        return true;
    }
}
