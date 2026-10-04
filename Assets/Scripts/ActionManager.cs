using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class ActionManager : MonoBehaviour
{
    [SerializeField] HandSystem handSystem;
    [SerializeField] WeaponController weaponController;
    private PlayerMovement playerMovement;
    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnToggleCombat(InputAction.CallbackContext context)
    {
        if(context.performed)
        {
            playerMovement.ToggleCombat();
        }
    }
    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        if(context.performed)
        {
            weaponController.Attack(WeaponController.AttackType.PrimaryAttack);
        }
    }
    public void OnSecondaryAttack(InputAction.CallbackContext context)
    {
        if(context.performed)
        {
            weaponController.Attack(WeaponController.AttackType.SecondaryAttack);
        }
    }
    public void OnSwapHands(InputAction.CallbackContext context)
    {
        // if there is a player stun state this shouldnt happen
        // pretty far out in terms of features to implement, though.
        if(context.performed)
        {
            handSystem.SwapHands();
            Debug.Log($"Swapped active hand. {handSystem.CurrentHandSlot}");
        }
    }
}
