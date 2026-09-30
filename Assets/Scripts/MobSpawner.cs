using System.Collections;
using UnityEngine;

public class MobSpawner : MonoBehaviour
{
    [SerializeField] private GameObject golemPrefab;
    [SerializeField] private GameObject huggerPrefab;
    [SerializeField] private GameObject player;
    [SerializeField] private Transform golemSpawnpoint;
    [SerializeField] private Transform huggerSpawnpoint;
    [SerializeField] private float respawnDelay = 3f;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void RespawnPair()
    {
        StartCoroutine(RespawnPairAfterDelay());
    }

    IEnumerator RespawnPairAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);

        GameObject golem = Instantiate(golemPrefab, golemSpawnpoint.position, Quaternion.identity);
        GameObject hugger = Instantiate(huggerPrefab, huggerSpawnpoint.position, Quaternion.identity);

        golem.GetComponent<MobMovement>().SetTarget(player);
        hugger.GetComponent<MobMovement>().SetTarget(player);
    }
}
