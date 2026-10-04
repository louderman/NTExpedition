using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponController : MonoBehaviour
{
    private PlayerMovement playerMovement;
    private UnarmedCombat unarmedFallback;
    public enum AttackType {PrimaryAttack, SecondaryAttack};
    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>(); 
        unarmedFallback = GetComponent<UnarmedCombat>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Attack(AttackType attackType)
    {
        // TODO: this command should be called with an active hand. find the weapon
        // in the active hand and perform the attack there. generally secondary attacks
        // tend to have weird properties though, perhaps check for an API that can bridge
        // the two functions together between inputmanager and the weapon itself?

        // add logic for checking for held weapon here

        if(playerMovement.combatMode == true)
        {
            unarmedFallback.ApplyInput(attackType);
        }
    }
    public void SwapHands()
    {
        // TODO: if single handed weapon, simply swap hand. defer to regular swap code
        // if two-handed, swap to an empty hand, unwield weapon
        // if there isnt any weapon being held then this shouldnt be active in the
        // first place since handsystem exists but given nearly everything except healing tools count as a 
        // weapon in my vision this should be an edge case
    }
}