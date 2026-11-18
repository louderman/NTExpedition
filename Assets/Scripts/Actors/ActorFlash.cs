using System.Collections;
using UnityEngine;

public class ActorFlash : MonoBehaviour
{
    [SerializeField] SpriteRenderer[] spriteRenderers;
    [SerializeField] float pulseSpeed;
    private WeaponController weaponController;
    private MaterialPropertyBlock block = new MaterialPropertyBlock();
    private float pingPong;
    void Start()
    {
        if (weaponController == null) weaponController = GetComponent<WeaponController>();
        if (block == null) block = new MaterialPropertyBlock();
    }

    // Update is called once per frame
    void Update()
    {
        // Re-acquire the cached references. An editor domain reload that lands while play mode
        // is running re-creates this component from the scene snapshot without re-running
        // Start(), which used to leave these null: GetPropertyBlock(null) then threw every
        // frame (measured: 567,507 ArgumentNullExceptions in one headless run).
        if (weaponController == null) weaponController = GetComponent<WeaponController>();
        if (weaponController == null) return;
        if (block == null) block = new MaterialPropertyBlock();

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
