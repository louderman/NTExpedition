using System.Collections;
using UnityEngine;

// One-shot muzzle flash, ported from Frontier Station 14's MuzzleFlashEffect
// (Resources/Prototypes/Entities/Objects/Weapons/Guns/Projectiles/projectiles.yml, lines 2-19):
// state "muzzle_bullet" of Objects/Weapons/Guns/Projectiles/projectiles.rsi, TimedDespawn 0.4 s,
// unshaded, drawn below mobs, rotated to the fire angle, spawned at the gun on every shot
// (SharedGunSystem.MuzzleFlash -> MuzzleFlashEvent -> client GunSystem.CreateEffect, which sets the
// effect's world rotation to the shot angle unchanged).
//
// Unity equivalent: spawn a short-lived sprite at the muzzle, rotate it so the art (authored pointing
// east) aims down the shot, draw it below mobs, then fade out and despawn 0.4 s
// later. It is spawned unparented in world space (like KineticProjectile's bolt) because the held
// item's own GameObject is inactive while the player carries it.
public class MuzzleFlashEffect : MonoBehaviour
{
    public const string ObjectName = "Muzzle flash";
    public const float Lifetime = .4f;
    // BelowMobs draw depth; the project already uses -1 for below-mob effects (see WeaponController's
    // bat shove visual).
    public const int SortingOrder = -1;

    public static MuzzleFlashEffect Spawn(Sprite sprite, Vector2 position, Vector2 direction,
        float lifetime = Lifetime)
    {
        if (sprite == null || direction.sqrMagnitude < .0001f) return null;

        var flash = new GameObject(ObjectName);
        flash.transform.position = position;
        // The muzzle_bullet art is authored pointing east -- hot core at the base, flame streak to the
        // right (alpha 31 px wide x 23 px tall) -- so the plain travel angle aims it straight down the
        // shot: Atan2 is already east-based and needs no offset. The chronobolt's +90 conversion is
        // specific to that sprite, whose art is authored pointing south (alpha 5 px wide x 29 px tall);
        // reusing it for the horizontal muzzle_bullet art turned the flash 90 degrees sideways,
        // perpendicular to the shot.
        flash.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

        var renderer = flash.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = SortingOrder;
        // Upstream marks the effect unshaded. This project renders URP 2D sprites with the default
        // sprite material, which is already unlit, so the flash ignores scene lighting as-is.

        var effect = flash.AddComponent<MuzzleFlashEffect>();
        effect.Initialize(renderer, lifetime);
        return effect;
    }

    SpriteRenderer flashRenderer;
    Color baseColor;
    float lifetime;
    float elapsed;

    void Initialize(SpriteRenderer renderer, float effectLifetime)
    {
        flashRenderer = renderer;
        baseColor = renderer.color;
        lifetime = Mathf.Max(.01f, effectLifetime);
        StartCoroutine(Play());
    }

    // TimedDespawn(0.4) equivalent; the client-side presentation is a short expand-and-fade.
    IEnumerator Play()
    {
        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lifetime);
            if (flashRenderer != null)
            {
                var color = baseColor;
                color.a = baseColor.a * (1f - progress);
                flashRenderer.color = color;
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, progress);
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
