using System.Collections;
using UnityEngine;

public class ActorFlash : MonoBehaviour
{
    [SerializeField] SpriteRenderer[] spriteRenderers;
    [SerializeField] float pulseSpeed;
    private WeaponController weaponController;
    private MaterialPropertyBlock block;
    private float pingPong;
    void Start()
    {
        weaponController = GetComponent<WeaponController>();
        block = new MaterialPropertyBlock();
    }

    // Update is called once per frame
    void Update()
    {
        if(!weaponController.IsCharging) pingPong =  0f;
        else pingPong = Mathf.PingPong(Time.time * pulseSpeed, 1f);
        PlayFlash(pingPong);
    }

    void PlayFlash(float flashAmount)
    {
        foreach(SpriteRenderer renderer in spriteRenderers)
        {
            renderer.GetPropertyBlock(block);
            block.SetFloat("_FlashAmount", flashAmount);
            renderer.SetPropertyBlock(block);
        }
    }
}
