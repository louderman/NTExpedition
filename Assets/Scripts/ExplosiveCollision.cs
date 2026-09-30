using System;
using System.Reflection.Metadata.Ecma335;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Jobs;

public class ExplosiveCollision : MonoBehaviour
{
    [SerializeField] Boolean isExplosive;
    [SerializeField] GameObject explosionFX;
    private bool explosionTriggered;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Debug.Log($"Collision callback: {collision.gameObject.name}");    
        if(explosionTriggered == true)
        {
            return;
        }
        else
        {
            if(collision.gameObject.CompareTag("FleshGolem") == true)
            {
                GameObject effect = Instantiate(explosionFX, transform.localPosition, transform.localRotation);
                Destroy(effect, 0.6f);
            }
        }
    }
}
