using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class UnarmedCombat : MonoBehaviour
{
    private Boolean attackDebounce = false;
    [SerializeField] float attackRange;
    [SerializeField] float attackDamage;
    [SerializeField] float attackCooldown;
    [SerializeField] float shoveDistance;

    // AI-generated debug visualization only; mirrors the shove query.
    private Vector2 debugBoxCenter;
    private float debugBoxAngle;
    private Vector2 debugBoxSize;
    private float debugBoxUntil;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // draft out inputs
        // and hope this works!
        // also btw to future me
        // x == false can be represented as !x
        if(Mouse.current.leftButton.wasPressedThisFrame == true && !attackDebounce)
        {
            Punch();
        }
        if(Mouse.current.rightButton.wasPressedThisFrame == true && !attackDebounce)
        {
            Shove();
        }
    }

    void Punch()
    {
        // TODO: punch, raycast, all that jazz
    }

    void Shove()
    {
        Vector2 mouseScreen = Mouse.current.position.ReadValue(); // reads pixel perfect position of mouse
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouseScreen); // converts that to an actual point in realspace
        Vector2 toMouse = mouseWorld - transform.position; // constructs the vector from player to mouse
        Vector2 godHelpMe = new Vector2(transform.position.x, transform.position.y);
        Vector2 hitboxPosition = godHelpMe + toMouse.normalized * (attackRange / 2);
        float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
        Collider2D[] allHit = Physics2D.OverlapBoxAll(hitboxPosition, new Vector2(attackRange, 1), angle);
        Debug.Log($"Entities hit: {allHit}");

        // AI-generated debug visualization only: show this shove box for one second.
        debugBoxCenter = hitboxPosition;
        debugBoxAngle = angle;
        debugBoxSize = new Vector2(attackRange, 1);
        debugBoxUntil = Time.time + 1f;
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || Time.time > debugBoxUntil) return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(debugBoxCenter,
            Quaternion.Euler(0f, 0f, debugBoxAngle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, debugBoxSize);
        Gizmos.matrix = previousMatrix;
    }
}
