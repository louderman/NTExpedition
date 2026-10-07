using UnityEngine;

public class MobDeath : MonoBehaviour
{
    private Health mobHealth;
    [SerializeField] private int deathReward;
    [SerializeField] private ScoreManager scoreManager;
    private MobSpawner huggerSpawner;

    void Awake()
    {
        mobHealth = GetComponent<Health>();
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
        scoreManager.AddScore(deathReward);
        if (huggerSpawner != null) huggerSpawner.RespawnHugger();
        Destroy(gameObject);
    }
}
