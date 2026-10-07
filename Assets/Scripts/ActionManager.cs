using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class ActionManager : MonoBehaviour
{
    [SerializeField] HandSystem handSystem;
    [SerializeField] WeaponController weaponController;
    public Vector2 crosshairPosition;
    private PlayerMovement playerMovement;
    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        if(context.performed)
        {
            playerMovement.moveVelocity = context.ReadValue<Vector2>();
        }
        if(context.canceled)
        {
            playerMovement.moveVelocity = Vector2.zero;
        }
    }
    public void OnPoint(InputAction.CallbackContext context)
    {
        crosshairPosition = context.ReadValue<Vector2>();
    }

    public void OnToggleCombat(InputAction.CallbackContext context)
    {
        if(context.performed)
        {
            if(playerMovement.combatMode)
            {
                weaponController.Attack(WeaponController.AttackType.PrimaryAttack, WeaponController.AttackPower.CancelCharge); // misleading af method name, this just cancels any charging that was done while in combat.
            }
            playerMovement.ToggleCombat();
        }
    }
    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        Debug.Log($"Printing sum shit {context.phase}, {context.interaction}");
        if(context.interaction is TapInteraction && context.performed)
        {
            weaponController.Attack(WeaponController.AttackType.PrimaryAttack, WeaponController.AttackPower.Quick);
        }
        if(context.interaction is SlowTapInteraction && context.started)
        {
            weaponController.Attack(WeaponController.AttackType.PrimaryAttack, WeaponController.AttackPower.BeginCharge);
        }
        if(context.interaction is SlowTapInteraction && context.performed)
        {
            weaponController.Attack(WeaponController.AttackType.PrimaryAttack, WeaponController.AttackPower.Charged);
        }
        if(context.interaction is SlowTapInteraction && context.canceled)
        {
            weaponController.Attack(WeaponController.AttackType.PrimaryAttack, WeaponController.AttackPower.Quick);
        }
    }
    public void OnSecondaryAttack(InputAction.CallbackContext context)
    {
        if(context.performed)
        {
            weaponController.Attack(WeaponController.AttackType.SecondaryAttack, WeaponController.AttackPower.Quick);
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
