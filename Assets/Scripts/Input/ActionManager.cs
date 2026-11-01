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
    private ExpeditionerStateController actorState;
    private InputAction releaseItemAction;
    void OnEnable()
    {
        var playerInput = GetComponent<PlayerInput>();
        releaseItemAction = playerInput != null && playerInput.actions != null
            ? playerInput.actions.FindAction("Player/ReleaseItem") : null;
        if (releaseItemAction != null) releaseItemAction.performed += OnReleaseItem;
    }
    void OnDisable()
    {
        if (releaseItemAction != null) releaseItemAction.performed -= OnReleaseItem;
        releaseItemAction = null;
    }
    public void OnReleaseItem(InputAction.CallbackContext context)
    {
        if (!context.performed || Time.timeScale == 0 || handSystem == null) return;
        var keyboard = context.control.device as Keyboard;
        bool throwing = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        handSystem.ReleaseActiveItem(throwing, crosshairPosition);
    }
    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        actorState = GetComponent<ExpeditionerStateController>();
    }

    // Lab 5: the state machine's EventAction trigger. Input is still sanitised here; the actor FSM
    // decides whether the current state has an Attack action to cast (only Armed does).
    public void OnPowerupAttack(InputAction.CallbackContext context)
    {
        if (!context.performed || Time.timeScale == 0f) return;
        if (actorState != null) actorState.Fire();
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
