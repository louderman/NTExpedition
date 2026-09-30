using UnityEngine;

public class ExplosiveCollision : MonoBehaviour
{
    [SerializeField] GameObject explosionFX;
    [SerializeField] private AudioClip explosionSound;
    private MobMovement mobMovement;
    private bool explosionTriggered;
    private MobSpawner mobSpawner;
    private ScoreManager scoreManager;
    void Start()
    {
        mobMovement = GetComponent<MobMovement>();
        explosionTriggered = false;
        mobSpawner = FindAnyObjectByType<MobSpawner>();
        scoreManager = FindAnyObjectByType<ScoreManager>();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // 
        if(explosionTriggered)
        {
            return; // it already exploded
        }
        else
        {
            if(collision.gameObject.CompareTag("FleshGolem") && mobMovement.isStunned)
            {
                Vector2 explosionPosition = collision.GetContact(0).point;
                GameObject effect = Instantiate(explosionFX, explosionPosition, Quaternion.identity);
                AudioSource.PlayClipAtPoint(explosionSound, explosionPosition);
                explosionTriggered = true;
                scoreManager.AddScore(5);
                mobSpawner.RespawnPair();
                Destroy(effect, 0.6f);
                Destroy(collision.gameObject);
                Destroy(gameObject);
            }
        }
    }
}
