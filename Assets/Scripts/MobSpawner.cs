using System.Collections;
using UnityEngine;

public class MobSpawner : MonoBehaviour
{
    [SerializeField] private GameObject huggerPrefab;
    [SerializeField] private GameObject initialHugger;
    [SerializeField] private GameObject player;
    [SerializeField] private Transform huggerSpawnpoint;
    [SerializeField] private float respawnDelay = 3f;
    [SerializeField] private int huggerDeathReward = 2;
    private ScoreManager scoreManager;
    private bool respawnPending;

    void Start()
    {
        scoreManager = FindAnyObjectByType<ScoreManager>();
        if (initialHugger != null) ConfigureHugger(initialHugger);
    }

    void ConfigureHugger(GameObject hugger)
    {
        hugger.GetComponent<MobMovement>().SetTarget(player);
        MobDeath death = hugger.GetComponent<MobDeath>();
        if (death == null) death = hugger.AddComponent<MobDeath>();
        death.ConfigureHugger(scoreManager, this, huggerDeathReward);
        ExplosiveCollision explosion = hugger.GetComponent<ExplosiveCollision>();
        if (explosion != null) explosion.enabled = false;
    }

    public void RespawnHugger()
    {
        if (respawnPending) return;
        respawnPending = true;
        StartCoroutine(RespawnHuggerAfterDelay());
    }

    // compatibility for the parked explosion script; this no longer spawns a Golem.
    public void RespawnPair()
    {
        RespawnHugger();
    }

    IEnumerator RespawnHuggerAfterDelay()
    {
        // The persistent spawner survives while the dead Hugger is destroyed.
        yield return new WaitForSeconds(respawnDelay);
        GameObject hugger = Instantiate(huggerPrefab, huggerSpawnpoint.position, Quaternion.identity);
        ConfigureHugger(hugger);
        respawnPending = false;
    }
}
