using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    private PlayerMovement playerMovement;
    private UnarmedCombat unarmedFallback;
    private HandSystem handSystem;
    private ActionManager actionManager;
    private Rigidbody2D playerBody;
    private bool weaponAttackDebounce;
    private bool weaponCharging;
    private Coroutine swingCoroutine;
    private GameObject activeWeaponVisual;
    public enum AttackType {PrimaryAttack, SecondaryAttack};
    public enum AttackPower {Quick, BeginCharge, Charged, CancelCharge};
    public bool IsCharging => HasActiveMeleeWeapon ? weaponCharging : unarmedFallback != null && unarmedFallback.IsCharging;
    public bool HasActiveMeleeWeapon => ActiveWeaponDefinition != null;
    public bool IsSwinging => swingCoroutine != null;
    public Vector2 LastAttackDirection { get; private set; }
    ItemDefinition ActiveWeaponDefinition
    {
        get
        {
            if (handSystem == null) return null;
            var item = handSystem.HeldItem(handSystem.CurrentHandSlot);
            return item != null && item.Definition != null && item.Definition.IsMeleeWeapon ? item.Definition : null;
        }
    }

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        unarmedFallback = GetComponent<UnarmedCombat>();
        handSystem = GetComponent<HandSystem>();
        actionManager = GetComponent<ActionManager>();
        playerBody = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (handSystem == null) handSystem = GetComponent<HandSystem>();
        if (handSystem != null)
        {
            handSystem.HandChanged += OnHandChanged;
            handSystem.HeldItemChanged += OnHeldItemChanged;
        }
    }

    void OnDisable()
    {
        if (handSystem != null)
        {
            handSystem.HandChanged -= OnHandChanged;
            handSystem.HeldItemChanged -= OnHeldItemChanged;
        }
        CancelWeaponState();
    }

    public void Attack(AttackType attackType, AttackPower attackContext)
    {
        if (attackContext == AttackPower.CancelCharge)
        {
            weaponCharging = false;
            unarmedFallback?.ApplyInput(attackType, attackContext);
            return;
        }

        if (playerMovement == null || !playerMovement.combatMode) return;
        var weapon = ActiveWeaponDefinition;
        if (weapon == null || attackType == AttackType.SecondaryAttack)
        {
            unarmedFallback?.ApplyInput(attackType, attackContext);
            return;
        }

        if (attackContext == AttackPower.BeginCharge)
        {
            if (!weaponAttackDebounce) weaponCharging = true;
            return;
        }

        if (attackContext == AttackPower.Charged)
        {
            bool wasCharging = weaponCharging;
            weaponCharging = false;
            if (!wasCharging) return;
        }
        else if (attackContext == AttackPower.Quick)
        {
            weaponCharging = false;
        }

        if (weaponAttackDebounce) return;
        Vector2 direction = AimDirection();
        weaponAttackDebounce = true;
        swingCoroutine = StartCoroutine(Swing(weapon, direction));
        StartCoroutine(Cooldown(weapon.MeleeCooldown));
    }

    public void SwapHands()
    {
        // TODO: if single handed weapon, simply swap hand. defer to regular swap code
        // if two-handed, swap to an empty hand, unwield weapon
        // if there isnt any weapon being held then this shouldnt be active in the
        // first place since handsystem exists but given nearly everything except healing tools count as a
        // weapon in my vision this should be an edge case
    }

    Vector2 AimDirection()
    {
        if (Camera.main == null || actionManager == null) return Vector2.down;
        Vector3 screen = new(actionManager.crosshairPosition.x, actionManager.crosshairPosition.y,
            -Camera.main.transform.position.z);
        Vector2 direction = (Vector2)Camera.main.ScreenToWorldPoint(screen) - (Vector2)transform.position;
        return direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.down;
    }

    IEnumerator Swing(ItemDefinition weapon, Vector2 direction)
    {
        LastAttackDirection = direction;
        activeWeaponVisual = new GameObject("Active melee swing");
        activeWeaponVisual.transform.SetParent(transform, false);
        var renderer = activeWeaponVisual.AddComponent<SpriteRenderer>();
        renderer.sprite = weapon.ItemIcon;
        renderer.sortingOrder = direction.y < 0f ? 20 : 7;

        float aimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float startAngle = aimAngle + weapon.MeleeSwingArc * .5f;
        float endAngle = aimAngle - weapon.MeleeSwingArc * .5f;
        float elapsed = 0f;
        bool hitApplied = false;
        while (elapsed < weapon.MeleeSwingDuration)
        {
            if (ActiveWeaponDefinition != weapon) break;
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / weapon.MeleeSwingDuration);
            float angle = Mathf.Lerp(startAngle, endAngle, Mathf.SmoothStep(0f, 1f, progress));
            Vector2 radial = new(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            activeWeaponVisual.transform.localPosition = radial * .22f;
            activeWeaponVisual.transform.localRotation = Quaternion.Euler(0f, 0f, angle - weapon.MeleeSpriteAngle);
            renderer.sortingOrder = radial.y < 0f ? 20 : 7;
            if (!hitApplied && progress >= .45f)
            {
                hitApplied = true;
                ApplyMeleeHit(weapon, direction);
            }
            yield return null;
        }
        if (activeWeaponVisual != null) Destroy(activeWeaponVisual);
        activeWeaponVisual = null;
        swingCoroutine = null;
    }

    void ApplyMeleeHit(ItemDefinition weapon, Vector2 direction)
    {
        Vector2 center = (Vector2)transform.position + direction * (weapon.MeleeRange * .5f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        var hits = Physics2D.OverlapBoxAll(center, new Vector2(weapon.MeleeRange, .9f), angle);
        var damagedBodies = new HashSet<Rigidbody2D>();
        foreach (var hit in hits)
        {
            var body = hit.attachedRigidbody;
            if (body == null || body == playerBody || !damagedBodies.Add(body)) continue;
            var health = body.GetComponent<Health>();
            if (health == null || health.CurrentHP <= 0f) continue;
            var movement = body.GetComponent<MobMovement>();
            if (movement != null) movement.OnKnockback(direction * weapon.MeleeKnockback, .12f);
            health.TakeDamage(weapon.MeleeDamage * (GetComponent<PlayerPowerups>()?.DamageMultiplier ?? 1f));
        }
    }

    IEnumerator Cooldown(float duration)
    {
        yield return new WaitForSeconds(duration);
        weaponAttackDebounce = false;
    }

    void OnHandChanged(HandSystem.HandSlot hand) { CancelWeaponState(); }
    void OnHeldItemChanged(HandSystem.HandSlot hand, ItemComponent item)
    {
        if (hand == handSystem.CurrentHandSlot) CancelWeaponState();
    }

    void CancelWeaponState()
    {
        weaponCharging = false;
        if (swingCoroutine != null) StopCoroutine(swingCoroutine);
        swingCoroutine = null;
        if (activeWeaponVisual != null) Destroy(activeWeaponVisual);
        activeWeaponVisual = null;
    }
}