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
    private Coroutine batShoveCoroutine;
    private GameObject activeWeaponVisual;
    private GameObject activeBatShoveVisual;
    private GameObject activeRangedVisual;
    private SpriteRenderer activeRangedPrimaryRenderer;
    private SpriteRenderer activeRangedGlowRenderer;
    private ItemComponent visualizedRangedItem;
    public enum AttackType {PrimaryAttack, SecondaryAttack};
    public enum AttackPower {Quick, BeginCharge, Charged, CancelCharge};
    public bool IsCharging => HasActiveMeleeWeapon ? weaponCharging : unarmedFallback != null && unarmedFallback.IsCharging;
    public bool HasActiveMeleeWeapon => ActiveWeaponDefinition != null;
    public bool HasActiveRangedWeapon => ActiveRangedWeapon != null;
    public bool IsSwinging => swingCoroutine != null;
    public bool IsBatShoving => batShoveCoroutine != null;
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
    ItemComponent ActiveItem => handSystem != null ? handSystem.HeldItem(handSystem.CurrentHandSlot) : null;
    RangedWeapon ActiveRangedWeapon
    {
        get
        {
            var item = ActiveItem;
            return item != null ? item.GetComponent<RangedWeapon>() : null;
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
            handSystem.WieldChanged += OnWieldChanged;
        }
    }

    void OnDisable()
    {
        if (handSystem != null)
        {
            handSystem.HandChanged -= OnHandChanged;
            handSystem.HeldItemChanged -= OnHeldItemChanged;
            handSystem.WieldChanged -= OnWieldChanged;
        }
        CancelWeaponState();
        DestroyRangedVisual();
    }

    void LateUpdate()
    {
        RefreshRangedVisual();
        if (activeRangedVisual == null) return;
        var ranged = ActiveRangedWeapon;
        Vector2 direction = AimDirection();
        bool wielded = handSystem.IsTwoHandWielded(ActiveItem);
        var facing = HeldFacing(playerMovement != null && !playerMovement.combatMode
            ? playerMovement.FacingDirection
            : direction);
        bool hasHeldArt = ranged.HasHeldSprites(wielded, handSystem.CurrentHandSlot);
        if (hasHeldArt)
            UpdateHeldRangedVisual(ranged, wielded, facing);
        else
            UpdateFallbackRangedVisual(direction);
    }

    // SS14 parity: two-handing the active ranged weapon is a hand state, not an attack, so it works
    // outside combat mode too (LateUpdate picks the wielded art either way). Returns true when this
    // input belonged to the wield system, so callers know not to fall through to another action.
    public bool TryToggleTwoHandWield()
    {
        var item = ActiveItem;
        var ranged = ActiveRangedWeapon;
        if (handSystem == null || item == null || ranged == null || !ranged.SupportsTwoHandedUse) return false;
        if (handSystem.IsTwoHandWielded(item)) handSystem.TryUnwield(item);
        else handSystem.TryWieldActiveItem(item);
        return true;
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
        var ranged = ActiveRangedWeapon;
        if (ranged != null)
        {
            if (attackType == AttackType.SecondaryAttack)
            {
                if (!TryToggleTwoHandWield()) unarmedFallback?.ApplyInput(attackType, attackContext);
                return;
            }
            if (attackContext == AttackPower.BeginCharge) return;
            Vector2 rangedDirection = AimDirection();
            float multiplier = GetComponent<BuffStateController>()?.DamageMultiplier ?? 1f;
            bool wielded = handSystem.IsTwoHandWielded(ActiveItem);
            if (ranged.TryFire((Vector2)transform.position + rangedDirection * .65f,
                rangedDirection, playerBody, multiplier, wielded))
                LastAttackDirection = ranged.LastShotDirection;
            return;
        }
        var weapon = ActiveWeaponDefinition;
        if (weapon == null)
        {
            unarmedFallback?.ApplyInput(attackType, attackContext);
            return;
        }
        if (attackType == AttackType.SecondaryAttack)
        {
            bool shoved = unarmedFallback != null
                && unarmedFallback.ApplyInput(attackType, attackContext, showShoveFx: false);
            if (shoved)
            {
                Vector2 shoveDirection = AimDirection();
                batShoveCoroutine = StartCoroutine(BatShove(weapon, shoveDirection));
            }
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
        handSystem?.SwapHands();
    }

    Vector2 AimDirection()
    {
        if (Camera.main == null || actionManager == null) return Vector2.down;
        Vector3 screen = new(actionManager.crosshairPosition.x, actionManager.crosshairPosition.y,
            -Camera.main.transform.position.z);
        Vector2 direction = (Vector2)Camera.main.ScreenToWorldPoint(screen) - (Vector2)transform.position;
        return direction.sqrMagnitude > .0001f ? direction.normalized : Vector2.down;
    }

    RangedWeapon.HeldDirection HeldFacing(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            return direction.x >= 0f ? RangedWeapon.HeldDirection.East : RangedWeapon.HeldDirection.West;
        return direction.y >= 0f ? RangedWeapon.HeldDirection.North : RangedWeapon.HeldDirection.South;
    }

    void UpdateHeldRangedVisual(RangedWeapon ranged, bool wielded, RangedWeapon.HeldDirection facing)
    {
        activeRangedVisual.transform.localPosition = Vector3.zero;
        activeRangedVisual.transform.localRotation = Quaternion.identity;
        activeRangedPrimaryRenderer.sprite = ranged.HeldSprite(wielded, handSystem.CurrentHandSlot, facing);
        activeRangedPrimaryRenderer.sortingOrder = facing == RangedWeapon.HeldDirection.South ? 20 : 7;
        if (activeRangedGlowRenderer != null) activeRangedGlowRenderer.enabled = false;
    }

    void UpdateFallbackRangedVisual(Vector2 direction)
    {
        var item = ActiveItem;
        activeRangedPrimaryRenderer.sprite = item != null && item.Definition != null ? item.Definition.ItemIcon : null;
        if (activeRangedGlowRenderer != null) activeRangedGlowRenderer.enabled = true;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        activeRangedVisual.transform.localPosition = direction * .48f;
        activeRangedVisual.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        foreach (var renderer in activeRangedVisual.GetComponentsInChildren<SpriteRenderer>())
            if (renderer.enabled) renderer.sortingOrder = direction.y < 0f ? 20 : 7;
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

    IEnumerator BatShove(ItemDefinition weapon, Vector2 direction)
    {
        LastAttackDirection = direction;
        activeBatShoveVisual = new GameObject("Active bat shove");
        activeBatShoveVisual.transform.SetParent(transform, false);
        var renderer = activeBatShoveVisual.AddComponent<SpriteRenderer>();
        renderer.sprite = weapon.ItemIcon;
        renderer.sortingOrder = -1;

        float aimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Vector2 sideways = new(-direction.y, direction.x);
        Vector2 gripOffset = sideways * -.62f;
        activeBatShoveVisual.transform.localRotation = Quaternion.Euler(0f, 0f,
            aimAngle + 90f - weapon.MeleeSpriteAngle);

        float duration = Mathf.Max(.05f, weapon.MeleeSwingDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (ActiveWeaponDefinition != weapon) break;
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float extension = 1f - Mathf.Abs(progress * 2f - 1f);
            extension = Mathf.SmoothStep(0f, 1f, extension);
            Vector2 position = gripOffset + direction * Mathf.Lerp(.1f, .65f, extension);
            activeBatShoveVisual.transform.localPosition = position;
            yield return null;
        }
        if (activeBatShoveVisual != null) Destroy(activeBatShoveVisual);
        activeBatShoveVisual = null;
        batShoveCoroutine = null;
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
            health.TakeDamage(weapon.MeleeDamage * (GetComponent<BuffStateController>()?.DamageMultiplier ?? 1f));
        }
    }

    IEnumerator Cooldown(float duration)
    {
        yield return new WaitForSeconds(duration);
        weaponAttackDebounce = false;
    }

    void RefreshRangedVisual()
    {
        var item = ActiveItem;
        var ranged = item != null ? item.GetComponent<RangedWeapon>() : null;
        if (item == visualizedRangedItem && (ranged == null) == (activeRangedVisual == null)) return;
        DestroyRangedVisual();
        if (item == null || ranged == null || item.Definition == null || item.Definition.ItemIcon == null) return;

        visualizedRangedItem = item;
        activeRangedVisual = new GameObject("Held ranged weapon");
        activeRangedVisual.transform.SetParent(transform, false);
        activeRangedPrimaryRenderer = activeRangedVisual.AddComponent<SpriteRenderer>();
        activeRangedPrimaryRenderer.sprite = item.Definition.ItemIcon;
        if (ranged.GlowSprite != null)
        {
            var glow = new GameObject("Unshaded detail");
            glow.transform.SetParent(activeRangedVisual.transform, false);
            activeRangedGlowRenderer = glow.AddComponent<SpriteRenderer>();
            activeRangedGlowRenderer.sprite = ranged.GlowSprite;
        }
    }

    void DestroyRangedVisual()
    {
        if (activeRangedVisual != null) Destroy(activeRangedVisual);
        activeRangedVisual = null;
        activeRangedPrimaryRenderer = null;
        activeRangedGlowRenderer = null;
        visualizedRangedItem = null;
    }

    void OnHandChanged(HandSystem.HandSlot hand) { CancelWeaponState(); RefreshRangedVisual(); }
    void OnHeldItemChanged(HandSystem.HandSlot hand, ItemComponent item)
    {
        if (hand == handSystem.CurrentHandSlot) { CancelWeaponState(); RefreshRangedVisual(); }
    }
    void OnWieldChanged(ItemComponent item, bool wielded)
    {
        if (item == ActiveItem) RefreshRangedVisual();
    }

    void CancelWeaponState()
    {
        weaponCharging = false;
        if (swingCoroutine != null) StopCoroutine(swingCoroutine);
        if (batShoveCoroutine != null) StopCoroutine(batShoveCoroutine);
        swingCoroutine = null;
        batShoveCoroutine = null;
        if (activeWeaponVisual != null) Destroy(activeWeaponVisual);
        activeWeaponVisual = null;
        if (activeBatShoveVisual != null) Destroy(activeBatShoveVisual);
        activeBatShoveVisual = null;
    }
}