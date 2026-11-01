using System;
using UnityEngine;

// The second, separate state machine required by the checkoff: temporary buffs
// (suit overcharge and shield/invincibility) live here, not on the actor state machine.
public class BuffStateController : StateController
{
    [Header("Requested buff (consumed by this state machine's transitions)")]
    public BuffType currentBuffType = BuffType.Default;

    [Header("Scene references")]
    [SerializeField] private Health health;
    [SerializeField] private ExpeditionConfig config;

    [Header("ScriptableObject runtime data")]
    [SerializeField] private BoolVariable actorInvulnerable;
    [SerializeField] private IntGameEvent onBuffStateChanged;

    [Header("States (linked by the setup script)")]
    [SerializeField] private State buffDefault;
    [SerializeField] private State buffOvercharged;
    [SerializeField] private State buffInvincible;

    public float ActiveDuration { get; private set; }
    public float EnteredAt { get; private set; }

    public bool IsInvincible => currentState != null && string.Equals(currentState.name, "BuffInvincible", StringComparison.OrdinalIgnoreCase);
    public bool IsOvercharged => currentState != null && string.Equals(currentState.name, "BuffOvercharged", StringComparison.OrdinalIgnoreCase);

    public float SpeedMultiplier => IsOvercharged && config != null ? config.speedMultiplier : 1f;
    public float DamageMultiplier => IsOvercharged && config != null ? config.damageMultiplier : 1f;

    public float RemainingSeconds =>
        IsInvincible || IsOvercharged ? Mathf.Max(0f, ActiveDuration - (Time.time - EnteredAt)) : 0f;

    public override void Start()
    {
        base.Start();
        if (health == null) health = GetComponent<Health>();
        GameRestart();
    }

    public void ApplyBuff(BuffType type) => currentBuffType = type;

    // Called by the buff states' setup actions.
    public void OnBuffEntered(int buffIndex, float duration)
    {
        ActiveDuration = duration;
        EnteredAt = Time.time;
        if (actorInvulnerable != null) actorInvulnerable.SetValue(buffIndex == (int)BuffState.BuffInvincible);
        if (onBuffStateChanged != null) onBuffStateChanged.Raise(buffIndex);
    }

    // Response for the OnGameRestart SimpleGameEventListener.
    public void GameRestart()
    {
        currentBuffType = BuffType.Default;
        ActiveDuration = 0f;
        EnteredAt = 0f;
        StopFlicker();
        if (actorInvulnerable != null) actorInvulnerable.SetValue(false);
        TransitionToState(startState != null ? startState : buffDefault);
        if (onBuffStateChanged != null) onBuffStateChanged.Raise((int)BuffState.BuffDefault);
    }
}
