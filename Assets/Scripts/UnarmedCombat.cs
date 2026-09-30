using System;
using System.Collections;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

public class UnarmedCombat : MonoBehaviour
{
    private Boolean attackDebounce = false;
    [SerializeField] float attackRange;
    [SerializeField] float attackDamage;
    [SerializeField] float attackCooldown;
    [SerializeField] float shoveStrength;
    [SerializeField] float shoveDuration;
    [SerializeField] private AudioSource shoveAudio;
    private Rigidbody2D protagBody;
    private PlayerMovement playerMovement;

    // AI-generated debug visualization only; mirrors the shove query.
    private Vector2 debugBoxCenter;
    private float debugBoxAngle;
    private Vector2 debugBoxSize;
    private float debugBoxUntil;

    void Start()
    {
        protagBody = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
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
            // punch isn't done yet, but not necessary for checkoff
            if(playerMovement.combatMode == true)
            {
                Punch();
                attackDebounce = true;
                StartCoroutine(Cooldown(attackCooldown, true));
            }
        }
        if(Mouse.current.rightButton.wasPressedThisFrame == true && !attackDebounce)
        {
            if(playerMovement.combatMode == true)
            {
                Shove();
                attackDebounce = true;
                StartCoroutine(Cooldown(attackCooldown, true));
            }
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

        // AI-generated debug visualization only: show this shove box for one second.
        debugBoxCenter = hitboxPosition;
        debugBoxAngle = angle;
        debugBoxSize = new Vector2(attackRange, 1);
        debugBoxUntil = Time.time + 1f;
        // end of AI generated code

        Vector2 impulseVector = toMouse.normalized * shoveStrength;

        foreach(Collider2D hit in allHit)
        {
            if(hit.attachedRigidbody == protagBody || hit.attachedRigidbody == null)
            {
                continue;
            }
            Debug.Log($"Entities hit: {hit}");
            Rigidbody2D targetBody = hit.attachedRigidbody;
            MobMovement mobMovement = targetBody.GetComponent<MobMovement>();
            if(mobMovement != null)
            {
                mobMovement.OnShove(impulseVector, shoveDuration);
            }
            else
            {
                targetBody.AddForce(impulseVector, ForceMode2D.Impulse);
            }
        }
        if(allHit.Count<Collider2D>() > 0)
        {
            shoveAudio.Play();
        }
        SpriteRenderer spriteChild = protagBody.GetComponentInChildren<SpriteRenderer>();
        Vector3 originVector = spriteChild.transform.localPosition;
        Vector3 impulseVector3 = new(impulseVector.x, impulseVector.y, 0f);
        Vector3 destinationVector = spriteChild.transform.localPosition + impulseVector3.normalized;
        StartCoroutine(PlayShoveAnimation(originVector, destinationVector * 0.3f));
    }

    IEnumerator Cooldown(float time, Boolean debounceTriggered)
    {
        yield return new WaitForSeconds(time);
        if(debounceTriggered == true)
        {
            attackDebounce = false;
        }
        
    }

    IEnumerator Cooldown(float time)
    {
        yield return new WaitForSeconds(time);
    }

    IEnumerator PlayShoveAnimation(Vector3 originVector, Vector3 destinationVector)
    {
        yield return StartCoroutine(AnimateMoveSprite(originVector, destinationVector));
        yield return StartCoroutine(AnimateMoveSprite(destinationVector, originVector));      
    }

    IEnumerator AnimateMoveSprite(Vector3 originVector, Vector3 destinationVector)
    {
        SpriteRenderer[] protagSprites = protagBody.GetComponentsInChildren<SpriteRenderer>();
        float elapsed = 0f; // state survives between Coroutine resumesThe
        float duration = 0.1f; // hard coded shove animation; will be the same for melee attacks in general
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            foreach(SpriteRenderer sprite in protagSprites)
            {
                sprite.transform.localPosition =  Vector3.Lerp(originVector, destinationVector, progress);
            }
            yield return null; // resume loop on the next frame
        }
    }

    // ai generated code
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
    // end of ai generated code
}
