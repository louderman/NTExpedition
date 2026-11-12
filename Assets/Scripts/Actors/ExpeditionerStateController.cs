using UnityEngine;

// Tier appearance (the equivalent of the handout's SetupAnimatorAction, which swaps a
// RuntimeAnimatorController). NTExpedition draws the crew member with plain SpriteRenderers,
// so a tier is a sprite-set index plus a tint on the suit and helmet renderers.
[System.Serializable]
public class TierVisual
{
    public Color suitTint = Color.white;
    public Color helmetTint = Color.white;
}

// Actor state machine (the handout's MarioStateController). Attached to the player.
// It owns tier appearance, damage request handling, the ranged Attack EventAction and reset.
public class ExpeditionerStateController : StateController
{
    [Header("Requested powerup (consumed by this state machine's transitions)")]
    public PowerupType currentPowerupType = PowerupType.Default;

    [Header("Scene references")]
    [SerializeField] private Health health;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private BuffStateController buffs;

    [Header("ScriptableObject runtime data")]
    [SerializeField] private IntVariable actorTier;
    [SerializeField] private BoolVariable actorInvulnerable;
    [SerializeField] private BoolVariable playerFacingRight;
    [SerializeField] private IntGameEvent onActorTierChanged;
    [SerializeField] private SimpleGameEvent onPlayerDied;

    [Header("States (linked by the setup script)")]
    [SerializeField] private State baseState;
    [SerializeField] private State armoredState;
    [SerializeField] private State armedState;
    [SerializeField] private State recoveringState;
    [SerializeField] private State downedState;

    [Header("Tier appearance")]
    [SerializeField] private TierVisual[] tiers;

    [Header("Ranged attack (Armed state event action)")]
    [SerializeField] private float attackDegree = 45f;
    [SerializeField] private Sprite fallbackProjectileSprite;
    [SerializeField] private float fallbackDamage = 6f;
    [SerializeField] private float fallbackSpeed = 12f;
    [SerializeField] private float fallbackRange = 8f;

    public int ActiveTier { get; private set; }

    public bool IsInvulnerable
    {
        get
        {
            if (buffs != null && buffs.IsInvincible) return true;
            return currentState != null && string.Equals(currentState.name, "Recovering", System.StringComparison.OrdinalIgnoreCase);
        }
    }

    public bool IsDowned => currentState != null && currentState == downedState;

    public Color SuitTint => Tier(ActiveTier).suitTint;
    public Color HelmetTint => Tier(ActiveTier).helmetTint;

    TierVisual Tier(int index)
    {
        if (tiers != null && index >= 0 && index < tiers.Length) return tiers[index];
        return new TierVisual();
    }

    public override void Start()
    {
        base.Start();
        if (health == null) health = GetComponent<Health>();
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (buffs == null) buffs = GetComponent<BuffStateController>();
        RestoreTierFromRuntimeData();
    }

    // Values persist across scenes through the ScriptableObject, not through a persistent object:
    // a fresh player in the next scene reads the tier that the previous scene stored.
    void RestoreTierFromRuntimeData()
    {
        int tier = actorTier != null ? actorTier.Value : 0;
        State target = StateForTier(tier);
        TransitionToState(target != null ? target : startState);
    }

    State StateForTier(int tier)
    {
        switch (tier)
        {
            case 1: return armoredState;
            case 2: return armedState;
            case 3: return recoveringState;
            case 99: return downedState;
            default: return baseState != null ? baseState : startState;
        }
    }

    public void SetPowerup(PowerupType type) => currentPowerupType = type;

    // Response for the PowerupGameEventListener on this GameObject: the collector asks the
    // powerup to apply itself, exactly as in the handout's RequestPowerupEffect chain.
    public void RequestPowerupEffect(PowerupDefinition definition)
    {
        if (definition == null) return;
        if (!definition.CanApply(this)) return;
        definition.ApplyPowerup(this);
    }

    // Response for the IntGameEventListener listening to OnPlayerDamaged.
    public void ApplyDamage(int damage)
    {
        if (damage <= 0 || IsDowned) return;
        if (IsInvulnerable) return; // invincible states swallow the hit entirely
        if (health != null) health.TakeDamage(damage);
        SetPowerup(PowerupType.Damage); // the current state's transitions decide the consequence
    }

    // Response for the OnGameRestart SimpleGameEventListener.
    public void GameRestart()
    {
        currentPowerupType = PowerupType.Default;
        if (health != null) health.ResetToFull();
        if (actorInvulnerable != null) actorInvulnerable.SetValue(false);
        StopFlicker();
        SetActiveTier(0);
        TransitionToState(startState);
    }

    public void SetActiveTier(int tier)
    {
        ActiveTier = tier;
        if (actorTier != null) actorTier.SetValue(tier);
        if (onActorTierChanged != null) onActorTierChanged.Raise(tier);
    }

    // Called from the Downed state's setup action.
    public void EnterDowned()
    {
        if (health != null) health.Kill();
        if (actorInvulnerable != null) actorInvulnerable.SetValue(false);
        StopFlicker();
        if (onPlayerDied != null) onPlayerDied.Raise();
    }

    // Linked to ActionManager, which sanitises the raw input (handout: MarioStateController.Fire).
    public void Fire()
    {
        if (currentState == null) return;
        currentState.DoEventTriggeredActions(this, ActionType.Attack);
    }

    public void SpawnRangedAttack(float degree, int maxPrefabInScene, BoolVariable faceRight)
    {
        if (FindObjectsByType<KineticProjectile>(FindObjectsSortMode.None).Length >= maxPrefabInScene) return;
        bool facingRight = faceRight == null || faceRight.Value;
        Vector2 direction = CalculateDirection(degree, facingRight);
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        float multiplier = buffs != null ? buffs.DamageMultiplier : 1f;
        RangedWeapon weapon = GetComponentInChildren<RangedWeapon>(true);
        if (weapon != null)
            weapon.TryFire(transform.position, direction, body, multiplier);
        else
            KineticProjectile.Launch(transform.position, direction, body, fallbackDamage * multiplier,
                fallbackSpeed, fallbackRange, fallbackProjectileSprite);
    }

    public Vector2 CalculateDirection(float degrees, bool isFacingRight)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float x = Mathf.Cos(radians);
        float y = Mathf.Sin(radians);
        if (!isFacingRight) x = -x;
        return new Vector2(x, y);
    }

    public override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
    }
}
