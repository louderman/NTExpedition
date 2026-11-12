using UnityEngine;

// Lab 5 registry: one asset that lists the whole ScriptableObject graph so the graph is inspectable
// in one place.
[CreateAssetMenu(menuName = "Lab5/Lab5 registry", order = 0)]
public class Lab5Registry : ScriptableObject
{
    [Header("Event channels")]
    public SimpleGameEvent onGameRestart;
    public SimpleGameEvent onPlayerDied;
    public SimpleGameEvent onCollectibleSpawned;
    public SimpleGameEvent onCollectibleExpired;
    public SimpleGameEvent onSceneTransition;
    public IntGameEvent onPlayerDamaged;
    public IntGameEvent onScoreChanged;
    public IntGameEvent onBestScoreChanged;
    public IntGameEvent onActorTierChanged;
    public IntGameEvent onBuffStateChanged;
    public PowerupGameEvent onPowerupCollected;
    public PowerupGameEvent onBuffApplied;

    [Header("Runtime data")]
    public IntVariable runScore;
    public IntVariable bestScore;
    public IntVariable actorTier;
    public BoolVariable playerFacingRight;
    public BoolVariable actorInvulnerable;

    [Header("Config")]
    public ExpeditionConfig config;

    [Header("Actor state machine")]
    public State actorRemainInState;
    public State actorBase;
    public State actorArmored;
    public State actorArmed;
    public State actorRecovering;
    public State actorDowned;

    [Header("Buff state machine")]
    public State buffDefault;
    public State buffOvercharged;
    public State buffInvincible;

    [Header("Powerups")]
    public PowerupDefinition suitKit;
    public PowerupDefinition kineticCore;
    public PowerupDefinition repair;
    public PowerupDefinition suitOvercharge;
    public PowerupDefinition shield;
}
