using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class ActionManager : MonoBehaviour
{
    [SerializeField] HandSystem handSystem;
    [SerializeField] WeaponController weaponController;
    [SerializeField] ItemDragSystem dragSystem;
    public Vector2 crosshairPosition;
    private PlayerMovement playerMovement;
    private ExpeditionerStateController actorState;
    private InputAction releaseItemAction;
    private InputAction dragGrabAction;
    private InputAction dragPullAction;
    void OnEnable()
    {
        var playerInput = GetComponent<PlayerInput>();
        releaseItemAction = FindAction(playerInput, "Player/ReleaseItem");
        if (releaseItemAction != null) releaseItemAction.performed += OnReleaseItem;
        // Ctrl + left click grabs a world item, right click pulls it: both are ordinary bound buttons, so
        // they are subscribed here exactly like ReleaseItem instead of being listed in a scene's events.
        dragGrabAction = FindAction(playerInput, "Player/DragGrab");
        if (dragGrabAction != null) dragGrabAction.performed += OnDragGrab;
        dragPullAction = FindAction(playerInput, "Player/DragPull");
        if (dragPullAction != null)
        {
            dragPullAction.performed += OnDragPull;
            dragPullAction.canceled += OnDragPull;
        }
    }
    void OnDisable()
    {
        if (releaseItemAction != null) releaseItemAction.performed -= OnReleaseItem;
        releaseItemAction = null;
        if (dragGrabAction != null) dragGrabAction.performed -= OnDragGrab;
        dragGrabAction = null;
        if (dragPullAction != null)
        {
            dragPullAction.performed -= OnDragPull;
            dragPullAction.canceled -= OnDragPull;
        }
        dragPullAction = null;
    }
    static InputAction FindAction(PlayerInput playerInput, string path)
    {
        return playerInput != null && playerInput.actions != null ? playerInput.actions.FindAction(path) : null;
    }
    public void OnReleaseItem(InputAction.CallbackContext context)
    {
        if (!context.performed || Time.timeScale == 0 || handSystem == null) return;
        var keyboard = context.control.device as Keyboard;
        bool throwing = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        handSystem.ReleaseActiveItem(throwing, crosshairPosition);
    }

    // Ctrl + left click, SS14 style: grab the item under the cursor and pull it around the room without
    // using a hand. A second Ctrl+click on the same drag lets it go again.
    public void OnDragGrab(InputAction.CallbackContext context)
    {
        if (!context.performed || Time.timeScale == 0f || dragSystem == null) return;
        if (dragSystem.IsDragging) { dragSystem.Drop(); return; }
        dragSystem.TryGrab(crosshairPosition);
    }

    // Right button held = reel the dragged item toward the cursor; released = stop reeling, keep dragging.
    public void OnDragPull(InputAction.CallbackContext context)
    {
        if (dragSystem == null) return;
        if (context.performed) dragSystem.SetPull(true);
        else if (context.canceled) dragSystem.SetPull(false);
    }

    void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        actorState = GetComponent<ExpeditionerStateController>();
        // The drag system lives with the rest of the player's item plumbing. It is created on demand so every
        // scene that already carries the input layer gains the mechanic without a per-scene component edit.
        if (dragSystem == null) dragSystem = GetComponent<ItemDragSystem>();
        if (dragSystem == null) dragSystem = gameObject.AddComponent<ItemDragSystem>();
    }

    // Lab 5: the state machine's EventAction trigger. Input is still sanitised here; the actor FSM
    // decides whether the current state has an Attack action to cast (only Armed does).
    // Z: SS14's two-hand/wield key first, Lab 5's EventAction trigger otherwise. Wielding the active
    // item wins so a two-handed ranged weapon toggles its wielded state; everything else falls through
    // to the actor FSM, which only casts from states that declare an Attack action.
    public void OnPowerupAttack(InputAction.CallbackContext context)
    {
        if (!context.performed || Time.timeScale == 0f) return;
        if (weaponController != null && weaponController.TryToggleTwoHandWield()) return;
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
        if (Time.timeScale == 0f) return;
        if(context.performed)
        {
            if(playerMovement.combatMode)
            {
                weaponController.Attack(WeaponController.AttackType.PrimaryAttack, WeaponController.AttackPower.CancelCharge); // misleading af method name, this just cancels any charging that was done while in combat.
            }
            // Combat mode is a hands-busy state: an item being dragged is let go rather than following the
            // player through a fight (HandSystem refuses pickups in combat mode for the same reason).
            if (dragSystem != null) dragSystem.Drop();
            playerMovement.ToggleCombat();
        }
    }
    public void OnPrimaryAttack(InputAction.CallbackContext context)
    {
        if (Time.timeScale == 0f || ExpeditionMenu.PointerOverInterface())
        {
            weaponController.Attack(WeaponController.AttackType.PrimaryAttack, WeaponController.AttackPower.CancelCharge);
            return;
        }
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
        if (Time.timeScale == 0f || ExpeditionMenu.PointerOverInterface()) return;
        if(context.performed)
        {
            // While dragging, the right button reels the item in instead of firing a secondary attack.
            if (dragSystem != null && dragSystem.IsDragging) return;
            weaponController.Attack(WeaponController.AttackType.SecondaryAttack, WeaponController.AttackPower.Quick);
        }
    }
    public void OnSwapHands(InputAction.CallbackContext context)
    {
        if (Time.timeScale == 0f) return;
        // if there is a player stun state this shouldnt happen
        // pretty far out in terms of features to implement, though.
        if(context.performed)
        {
            weaponController.SwapHands();
            Debug.Log($"Swapped active hand. {handSystem.CurrentHandSlot}");
        }
    }
}
