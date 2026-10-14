using System;
using UnityEngine;

public class MobDeath : MonoBehaviour
{
    private Health mobHealth;
    private MobMovement mobMovement;
    private SpriteRenderer mobSprite;
    private Rigidbody2D mobBody;
    private Animator mobAnimator;
    [SerializeField] Sprite mobDeadSprite;
    [SerializeField] private int deathReward;
    [SerializeField] private ScoreManager scoreManager;
    private MobSpawner huggerSpawner;

    void Awake()
    {
        mobHealth = GetComponent<Health>();
        mobMovement = GetComponent<MobMovement>();
        mobBody = GetComponent<Rigidbody2D>();
        mobSprite = GetComponentInChildren<SpriteRenderer>();
        mobAnimator = GetComponentInChildren<Animator>();
    }

    void OnEnable()
    {
        mobHealth.Died += OnDeath;
    }

    void OnDisable()
    {
        mobHealth.Died -= OnDeath;
    }

    // AI-assisted: only Huggers receive this wiring; Golems keep their existing death behavior.
    public void ConfigureHugger(ScoreManager score, MobSpawner spawner, int reward)
    {
        scoreManager = score;
        huggerSpawner = spawner;
        deathReward = reward;
    }

    void OnDeath()
    {
        // AI-assisted: the newly loaded scene's serialized manager may be a discarded duplicate.
        ScoreManager.Instance.AddScore(deathReward);
        var flow = FindAnyObjectByType<ExpeditionFlow>();
        if (flow != null) flow.PlayEffect(flow.config.deathSound);
        mobMovement.StopAllCoroutines();
        mobMovement.enabled = false;
        mobBody.linearVelocity = new Vector2(0f, 0f);
        mobBody.simulated = false;
        mobAnimator.enabled = false;
        mobSprite.sprite = mobDeadSprite;
        if (huggerSpawner != null) huggerSpawner.RespawnHugger();
        Destroy(gameObject, 3f);
    }
}
