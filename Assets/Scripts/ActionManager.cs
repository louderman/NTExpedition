using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

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
