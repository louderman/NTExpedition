using UnityEngine;

// A short, non-ballistic 2D throw. Triggers (including pickups) are ignored;
// solid scene colliders stop the item rather than allowing it through walls.
public class ItemThrow : MonoBehaviour
{
    Vector2 direction;
    float speed, remaining;
    Transform thrower;
    public bool IsMoving => remaining > 0f;
    public void Launch(Vector2 aim, float unitsPerSecond, float distance, Transform owner)
    {
        direction = aim.normalized;
        speed = Mathf.Max(0f, unitsPerSecond);
        remaining = speed > 0f && direction.sqrMagnitude > 0 ? Mathf.Max(0f, distance) : 0f;
        thrower = owner;
    }
    void FixedUpdate()
    {
        if (!IsMoving) return;
        float step = Mathf.Min(remaining, speed * Time.fixedDeltaTime);
        var collider = GetComponent<Collider2D>();
        float radius = collider is CircleCollider2D circle ? circle.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)) : .1f;
        var hits = Physics2D.CircleCastAll(transform.position, radius, direction, step);
        float permitted = step;
        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.isTrigger || hit.collider.transform.IsChildOf(transform) || (thrower != null && hit.collider.transform.IsChildOf(thrower))) continue;
            permitted = Mathf.Min(permitted, Mathf.Max(0f, hit.distance - .01f));
        }
        transform.position += (Vector3)(direction * permitted);
        remaining = permitted < step ? 0f : Mathf.Max(0f, remaining - step);
    }
}
