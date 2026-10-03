using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponController : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void PrimaryAttack()
    {
        // TODO: this command should be called with an active hand. find the weapon
        // in the active hand and perform the attack there.
    }
    public void SecondaryAttack()
    {
        // TODO: this command should be called with an active hand. find the weapon
        // in the active hand and perform the attack there. generally secondary attacks
        // tend to have weird properties though, perhaps check for an API that can bridge
        // the two functions together between inputmanager and the weapon itself?
    }
    public void SwapHands()
    {
        // TODO: if single handed weapon, simply swap hand
        // if two-handed, swap to an empty hand, unwield weapon
        // if there isnt any weapon being held then this shouldnt be active in the
        // first place, but given nearly everything except healing tools count as a 
        // weapon in my vision this should be an edge case
    }
}