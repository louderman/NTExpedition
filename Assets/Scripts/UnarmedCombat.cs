using System;
using System.Collections;
using System.Linq;
using Unity.VisualScripting;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.InputSystem;

public class UnarmedCombat : MonoBehaviour
{
    private Boolean attackDebounce = false;
    [SerializeField] float attackRange;
    [SerializeField] float attackDamage;
    [SerializeField, Min(0f)] float punchKnockbackStrength = 0.5f;
    [SerializeField, Min(0f)] float punchKnockbackDuration = 0.12f;
    [SerializeField] float attackCooldown;
    [SerializeField] float shoveStrength;
    [SerializeField] float shoveDuration;
    [SerializeField] private AudioSource shoveAudio;
    [SerializeField] private AudioSource punchAudio;
    [SerializeField] private GameObject shoveFX;
    [SerializeField] private ScoreManager scoreManager;
    private Rigidbody2D protagBody;
    private PlayerMovement playerMovement;
    private ActionManager actionManager;
    private bool _isCharging = false;
    public bool IsCharging {get { return _isCharging;}}


    // AI-generated debug visualization only; mirrors the shove query.
    private Vector2 debugBoxCenter;
    private float debugBoxAngle;
    private Vector2 debugBoxSize;
    private float debugBoxUntil;

    void Start()
    {
        protagBody = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        actionManager = GetComponent<ActionManager>();
    }

    // Update is called once per frame
    void Update()
    {
    }
    
    public void ApplyInput(WeaponController.AttackType attackType, WeaponController.AttackPower attackContext)
    {
        if(attackContext == WeaponController.AttackPower.CancelCharge)
            {
                _isCharging = false;
                Debug.Log("Charge canceled.");
                return;
            }
        
        if(attackType == WeaponController.AttackType.PrimaryAttack)
        {
            if(attackContext == WeaponController.AttackPower.Quick)
            {
                _isCharging = false;
                if(attackDebounce) return;
                Debug.Log("Quick Punch!");
                Punch();
                attackDebounce = true;
                StartCoroutine(Cooldown(attackCooldown, true));
            }
            if(attackContext == WeaponController.AttackPower.BeginCharge && !attackDebounce)
            {
                ChargeStartup(); 
            }
            if(attackContext == WeaponController.AttackPower.Charged)
            {
                bool wasCharging = _isCharging; 
                _isCharging = false; 
                if(!wasCharging || attackDebounce) return; 
                Debug.Log("Charged Punch!"); 
                Punch(); // TODO; distinct feedback for charging the punch
                attackDebounce = true;
                StartCoroutine(Cooldown(attackCooldown, true));
            }
        
        }
        if(attackType == WeaponController.AttackType.SecondaryAttack && !attackDebounce)
        {
            Shove();
            attackDebounce = true;
            StartCoroutine(Cooldown(attackCooldown, true));
        }
    }

    void ChargeStartup()
    {
        _isCharging = true;
        Debug.Log("Charging...");
    }
    void Punch()
    {
        Vector2 mouseScreen = actionManager.crosshairPosition; // reads pixel perfect position of mouse
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouseScreen); // converts that to an actual point in realspace
        Vector2 toMouse = mouseWorld - transform.position; // constructs the vector from player to mouse
        Vector2 godHelpMe = new Vector2(transform.position.x, transform.position.y);
        Vector2 hitboxPosition = godHelpMe + toMouse.normalized * (attackRange / 2);
        float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
        Collider2D[] allHit = Physics2D.OverlapBoxAll(hitboxPosition, new Vector2(attackRange, 1), angle);

        Quaternion hitboxRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        GameObject effect = Instantiate(shoveFX, hitboxPosition, hitboxRotation);
        Destroy(effect, 0.3f);

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
            Health mobHealth = targetBody.GetComponent<Health>();
            if(mobHealth != null)
            {
                // AI-assisted: recoil only living damageable mobs; death response stays in MobDeath.
                if (mobHealth.CurrentHP > 0f && attackDamage > 0f)
                {
                    MobMovement movement = targetBody.GetComponent<MobMovement>();
                    if (movement != null)
                        movement.OnKnockback(toMouse.normalized * punchKnockbackStrength, punchKnockbackDuration);
                    mobHealth.TakeDamage(attackDamage);
                }
            }
            else
            {
                Debug.Log("you aint breaking this, dummy.");
            }
        }
        if(allHit.Count() > 0)
        {
            punchAudio.Play();
        }
        SpriteRenderer spriteChild = protagBody.GetComponentInChildren<SpriteRenderer>();
        Vector3 originVector = spriteChild.transform.localPosition;
        Vector3 impulseVector3 = new(impulseVector.x, impulseVector.y, 0f);
        Vector3 destinationVector = spriteChild.transform.localPosition + impulseVector3.normalized * 0.3f;
        StartCoroutine(PlayShoveAnimation(originVector, destinationVector));
    }

    void Shove()
    {
        Vector2 mouseScreen = actionManager.crosshairPosition; // reads pixel perfect position of mouse
        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(mouseScreen); // converts that to an actual point in realspace
        Vector2 toMouse = mouseWorld - transform.position; // constructs the vector from player to mouse
        Vector2 godHelpMe = new Vector2(transform.position.x, transform.position.y);
        Vector2 hitboxPosition = godHelpMe + toMouse.normalized * (attackRange / 2);
        float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
        Collider2D[] allHit = Physics2D.OverlapBoxAll(hitboxPosition, new Vector2(attackRange, 1), angle);

        Quaternion hitboxRotation = Quaternion.Euler(0f, 0f, angle - 90f);
        GameObject effect = Instantiate(shoveFX, hitboxPosition, hitboxRotation);
        Destroy(effect, 0.3f);

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
        if(allHit.Count() > 0)
        {
            shoveAudio.Play();
        }
        SpriteRenderer spriteChild = protagBody.GetComponentInChildren<SpriteRenderer>();
        Vector3 originVector = spriteChild.transform.localPosition;
        Vector3 impulseVector3 = new(impulseVector.x, impulseVector.y, 0f);
        Vector3 destinationVector = spriteChild.transform.localPosition + impulseVector3.normalized * 0.3f;
        StartCoroutine(PlayShoveAnimation(originVector, destinationVector));
    }


    // TODO: probably abstract these out, these are basically functions that are meant to play
    // regardless of the weapon.
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
