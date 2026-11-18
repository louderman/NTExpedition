using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class MobMovement : MonoBehaviour
{
    public float moveSpeed;
    public Boolean isStunned;
    private Coroutine recoveryCoroutine;
    private bool isRecoiling;
    private Coroutine recoilCoroutine;
    private Quaternion uprightRotation;
    [SerializeField] private SpriteRenderer mobRenderer;                                                                                                                                          
    [SerializeField] private Sprite[] mobSprites;
    [SerializeField] private GameObject protagObject;
    [SerializeField] private Animator mobAnimator;
    [SerializeField] private Rigidbody2D mobBody;
    [SerializeField] private bool lethal;
    [SerializeField] private bool directionless;
    [SerializeField, Min(0.1f)] private float aggroRadius = 6f;
    [SerializeField, Min(0.1f)] private float deaggroRadius = 10f;
    [SerializeField, Min(0.1f)] private float repathInterval = 0.3f;
    [SerializeField] private Tilemap navigationFloor;
    public bool IsAggroed { get; private set; }
    public bool IsLethal => lethal && !isStunned;
    public bool CanAct => isActiveAndEnabled && !isStunned && !isRecoiling;
    public Transform Target => protagObject != null ? protagObject.transform : null;
    public bool HoldPosition { get; set; }
    public event Action Interrupted;
    private GridPathfinder pathfinder;
    private Collider2D bodyCollider;
    private readonly List<Vector2> path = new();
    private int waypoint;
    private float nextPathTime;
    private enum Facing { South, North, East, West };
    void Awake()
    {
        bodyCollider = GetComponent<Collider2D>();
        if (navigationFloor == null)
        {
            foreach (Tilemap tilemap in FindObjectsByType<Tilemap>())
            {
                if (tilemap.gameObject.scene == gameObject.scene &&
                    tilemap.gameObject.layer == LayerMask.NameToLayer("Floor"))
                {
                    navigationFloor = tilemap;
                    break;
                }
            }
        }
        pathfinder = new GridPathfinder(navigationFloor);
    }

    void OnValidate()
    {
        aggroRadius = Mathf.Max(0.1f, aggroRadius);
        deaggroRadius = Mathf.Max(aggroRadius + 0.1f, deaggroRadius);
        repathInterval = Mathf.Max(0.1f, repathInterval);
    }
    void Start()
    {
        if (protagObject == null)
            SetTarget(FindAnyObjectByType<ExpeditionerStateController>()?.gameObject);
        recoveryCoroutine = null;
        uprightRotation = mobRenderer.transform.localRotation;
    }

    public void SetTarget(GameObject target)
    {
        protagObject = target;
        IsAggroed = false;
        HoldPosition = false;
        path.Clear();
        nextPathTime = 0f;
    }

    public void FaceTarget()
    {
        if (Target != null) ProcessFacing(Target.position - transform.position);
    }

    void ProcessFacing(Vector3 toTarget)
    {
        if (Mathf.Abs(toTarget.x) > Mathf.Abs(toTarget.y)) // check horizontal
        {
            if(toTarget.x >= 0)
            {
                DisplayFacing(Facing.East);
            }
            else
            {
                DisplayFacing(Facing.West);
            }
        }
        else
        {
            if(toTarget.y >= 0)
            {
                DisplayFacing(Facing.North);
            }
            else
            {
                DisplayFacing(Facing.South);
            }
        }

    }


    void DisplayFacing(Facing face)
    {
        if(mobAnimator != null)
        {
            if(!directionless)
            {
                int index = (int) face;
                mobAnimator.SetInteger("Facing", index);
            }
        }
        else
        {
            // fallback to static sprite handling
            if(!directionless)
            {
                int index = (int) face;
                mobRenderer.sprite = mobSprites[index];
            }
        }
        
    }

    void FixedUpdate()
    {
        float distance = protagObject == null ? float.PositiveInfinity :
            Vector2.Distance(transform.position, protagObject.transform.position);
        bool wasAggroed = IsAggroed;
        IsAggroed = protagObject != null && (IsAggroed
            ? distance <= Mathf.Max(deaggroRadius, aggroRadius + 0.1f)
            : distance <= aggroRadius);
        if (!IsAggroed || !wasAggroed)
        {
            path.Clear();
            nextPathTime = 0f;
        }

        // Chase and idle must both leave shove and recoil velocity alone.
        if (isStunned || isRecoiling) return;
        if (!IsAggroed || bodyCollider == null)
        {
            mobBody.linearVelocity = Vector2.zero;
            return;
        }
        if (HoldPosition)
        {
            mobBody.linearVelocity = Vector2.zero;
            FaceTarget();
            return;
        }

        Vector2 position = bodyCollider.bounds.center;
        Vector2 size = bodyCollider.bounds.size;
        Vector2 target = (Vector2)protagObject.transform.position +
            (position - (Vector2)transform.position);
        Vector2 destination = target;
        if (!pathfinder.CanTravel(position, target, size))
        {
            if (Time.time >= nextPathTime)
            {
                pathfinder.FindPath(position, target, size, path);
                // Rejoining a route must not send us back to the centre of the previous cell.
                waypoint = path.Count > 1 && pathfinder.CanTravel(position, path[1], size) ? 1 : 0;
                nextPathTime = Time.time + repathInterval;
            }
            while (waypoint < path.Count &&
                Vector2.Distance(position, path[waypoint]) <= 0.08f) waypoint++;
            if (waypoint >= path.Count)
            {
                mobBody.linearVelocity = Vector2.zero;
                return;
            }
            destination = path[waypoint];
        }
        else
        {
            path.Clear();
            nextPathTime = 0f;
        }

        Vector2 direction = destination - position;
        Vector2 step = Vector2.ClampMagnitude(direction, moveSpeed * Time.fixedDeltaTime);
        if (!pathfinder.CanTravel(position, position + step, size))
        {
            mobBody.linearVelocity = Vector2.zero;
            return;
        }
        mobBody.linearVelocity = step / Time.fixedDeltaTime;
        if (direction.sqrMagnitude > 0.001f) ProcessFacing(direction);
    }

    public void OnKnockback(Vector2 impulse, float duration)
    {
        if (duration <= 0f) return;
        Interrupted?.Invoke();
        if (recoilCoroutine != null) StopCoroutine(recoilCoroutine); // cancel the old expiry.
        isRecoiling = true; // prevent chase from erasing the impulse.
        mobBody.linearVelocity = Vector2.zero; // remove forward momentum before the small kick.
        mobBody.AddForce(impulse, ForceMode2D.Impulse);
        recoilCoroutine = StartCoroutine(RecoverFromKnockback(duration));
    }

    IEnumerator RecoverFromKnockback(float duration)
    {
        yield return new WaitForSeconds(duration);
        isRecoiling = false;
        recoilCoroutine = null;
    }

    public void OnShove(Vector2 impulseVector, float shoveDuration)
    {
        Interrupted?.Invoke();
        // probably will be expanded to include other things.
        // for now, only shove matters.
        mobBody.AddForce(impulseVector, ForceMode2D.Impulse);
        Quaternion startingRotation = mobRenderer.transform.localRotation;
        Quaternion finalRotation;
        if(isStunned == true)
        {
            if(recoveryCoroutine != null)
            {
                StopCoroutine(recoveryCoroutine);
                StartCoroutine(RecoverFromShove(shoveDuration));
            }
        }
        else
        {
            isStunned = true;
            if(Mathf.Abs(impulseVector.x) > Mathf.Abs(impulseVector.y))
            {
                if(impulseVector.x >= 0)
                {
                    finalRotation = Quaternion.Euler(0f, 0f, -90f);
                }
                else
                {
                    finalRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            else
            {
                if(impulseVector.y >= 0)
                {
                    finalRotation = Quaternion.Euler(0f, 0f, -90f);
                }
                else
                {
                    finalRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            recoveryCoroutine = StartCoroutine(RecoverFromShove(shoveDuration));
            StartCoroutine(AnimateRotateSprite(startingRotation, finalRotation, 0.3f));
        }
        
    }
    IEnumerator RecoverFromShove(float stunDuration)
    {
        yield return new WaitForSeconds(stunDuration);

        Quaternion currentRotation = mobRenderer.transform.localRotation;
        yield return StartCoroutine(AnimateRotateSprite(currentRotation, uprightRotation, 0.3f));

        isStunned = false;
        recoveryCoroutine = null;
    }
    IEnumerator AnimateRotateSprite(Quaternion start, Quaternion target, float duration)
    {
        float elapsed = 0f; // state survives between Coroutine resumes

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            mobRenderer.transform.localRotation = Quaternion.Slerp(start, target, progress);

            yield return null; // resume loop on the next frame
        }

        mobRenderer.transform.localRotation = target;
    }
}
