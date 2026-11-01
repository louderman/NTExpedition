using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed;
    private float distanceThisFrame;
    private Vector3 step;
    private Vector2 step2D;
    private int x;
    private int y;
    [SerializeField] private SpriteRenderer suitRenderer;                                                                                                                                          
    [SerializeField] private SpriteRenderer helmetRenderer; 
    [SerializeField] private Sprite[] suitSprites;
    [SerializeField] private Sprite[] helmetSprites;
    [SerializeField] private Rigidbody2D protagBody;
    // Lab 5: appearance and buff multipliers now come from the two state machines.
    [SerializeField] private ExpeditionerStateController stateController;
    [SerializeField] private BuffStateController buffState;
    [SerializeField] private BoolVariable playerFacingRight;
    private ActionManager actionManager;
    private enum Facing { South, North, East, West };
    private List<Facing> faceList = new();
    public Vector2 currentlyFacing = new(0f, 0f);
    public Boolean combatMode = false; 
    public Vector2 moveVelocity;
    private WeaponController weaponController;
    void Start()
    {
        actionManager = GetComponent<ActionManager>();
        weaponController = GetComponent<WeaponController>();
        if (stateController == null) stateController = GetComponent<ExpeditionerStateController>();
        if (buffState == null) buffState = GetComponent<BuffStateController>();
    }

    // Update is called once per frameI'm 
    void Update()
    {  
        // now for sprite facing
        ProcessFacing();

        // override if combat mode
        if (combatMode == true)
        {
            CombatModeFacing();
        }
        else
        {
            DisplayFacing();
        }

    }

    void ProcessFacing()
    {
        if (Keyboard.current == null || Time.timeScale == 0f) return;
        if (Keyboard.current.wKey.wasPressedThisFrame == true)
        { 
            SetFacing(Facing.North);
        }
        if (Keyboard.current.dKey.wasPressedThisFrame == true)
        { 
            SetFacing(Facing.East);
        }
        if (Keyboard.current.sKey.wasPressedThisFrame == true)
        { 
            SetFacing(Facing.South);
        }
        if (Keyboard.current.aKey.wasPressedThisFrame == true)
        { 
            SetFacing(Facing.West);
        }
        if (Keyboard.current.wKey.wasReleasedThisFrame == true)
        { 
            RemoveFacing(Facing.North);
        }
        if (Keyboard.current.dKey.wasReleasedThisFrame == true)
        { 
            RemoveFacing(Facing.East);
        }
        if (Keyboard.current.sKey.wasReleasedThisFrame == true)
        { 
            RemoveFacing(Facing.South);
        }
        if (Keyboard.current.aKey.wasReleasedThisFrame == true)
        { 
            RemoveFacing(Facing.West);
        }
    }

    // sprite facing handling when out of combat
    // functional programming abstractions so I know generally what each function does

    void SetFacing(Facing face)
    {
        faceList.Add(face);
    }

    void RemoveFacing(Facing face)
    {
        faceList.Remove(face);
    }

    void DisplayFacing()
    {
        if (faceList.Count > 0)
        {
            int index = (int) faceList[^1];
            ApplyFacing(index);
            UpdateFacingVariable(index == (int) Facing.East);
        }
    }

    // Lab 5: which sprite set is drawn is decided by the actor state machine's tier plus this
    // script's facing, so a tier change is visible without duplicating movement code.
    void ApplyFacing(int index)
    {
        index = Mathf.Clamp(index, 0, suitSprites.Length - 1);
        suitRenderer.sprite = suitSprites[index];
        helmetRenderer.sprite = helmetSprites[Mathf.Clamp(index, 0, helmetSprites.Length - 1)];
        if (stateController != null)
        {
            suitRenderer.color = stateController.SuitTint;
            helmetRenderer.color = stateController.HelmetTint;
        }
    }

    // Publishes the current facing to a ScriptableObject so a state machine action asset (which
    // cannot reference a scene component) can aim the ranged attack.
    void UpdateFacingVariable(bool facingRight)
    {
        if (playerFacingRight != null) playerFacingRight.SetValue(facingRight);
    }

    // special method to deal with sprite facing when combat mode is active
    // ideally if a player is shooting they shouldnt be shooting out their backsides
    // likely this script will be used for mob attacks in a modified way so im getting
    // this out of the way first

    void CombatModeFacing()
    {
        Vector2 mouseScreen = actionManager.crosshairPosition; // reads pixel perfect position of mouse
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouseScreen); // converts that to an actual point in realspace
        Vector2 toMouse = mouseWorld - transform.position; // constructs the vector from player to mouse
        currentlyFacing = toMouse;
        if (Mathf.Abs(toMouse.x) > Mathf.Abs(toMouse.y)) // check horizontal
        {
            if(toMouse.x >= 0)
            {
                CombatModeDisplayFacing(Facing.East);
            }
            else
            {
                CombatModeDisplayFacing(Facing.West);
            }
        }
        else
        {
            if(toMouse.y >= 0)
            {
                CombatModeDisplayFacing(Facing.North);
            }
            else
            {
                CombatModeDisplayFacing(Facing.South);
            }
        }

    }

    void CombatModeDisplayFacing(Facing face)
    {
        int index = (int) face;
        ApplyFacing(index);
        UpdateFacingVariable(index == (int) Facing.East);
    }

    public void ToggleCombat()
    {
        if(combatMode)
        {
            combatMode = false;
        }
        else
        {
            combatMode = true;
        }
    }

    private float PowerupSpeed => GetComponent<BuffStateController>()?.SpeedMultiplier ?? 1f;

    void MoveBody()
    {
        if(!weaponController.IsCharging)
        {
            protagBody.linearVelocity = moveVelocity.normalized * moveSpeed * PowerupSpeed;
        }
        else
        {
            protagBody.linearVelocity = moveVelocity.normalized * moveSpeed * PowerupSpeed * 0.35f; // change hardcoded multiplier to exposed number thru weapon controller later
        }

        // TODO: use this for nograv environments
        // medium priority, also this implementation makes the player slow as hell
        // distanceThisFrame = moveSpeed * Time.deltaTime;
        // protagBody.AddForce(step2D.normalized * distanceThisFrame);
    }
    void FixedUpdate()
    {
        MoveBody();
    }
}