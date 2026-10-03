using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// note, eventually I want to add a player detection system as well.
// for now we'll just have mobs that follow the player around.

public class MobMovement : MonoBehaviour
{
    public float moveSpeed;
    private float distanceThisFrame;
    private int x;
    private int y;
    public Boolean isStunned;
    private Coroutine recoveryCoroutine;
    private Quaternion uprightRotation;
    [SerializeField] private SpriteRenderer mobRenderer;                                                                                                                                          
    [SerializeField] private Sprite[] mobSprites;
    [SerializeField] private GameObject protagObject;
    [SerializeField] private Animator mobAnimator;
    [SerializeField] private Rigidbody2D mobBody;
    [SerializeField] private bool lethal;
    [SerializeField] private int shoveScore;
    [SerializeField] private bool directionless;
    public bool IsLethal => lethal && !isStunned;
    public int ShoveScore => shoveScore;
    private enum Facing { South, North, East, West };
    private List<Facing> faceList = new();
    void Start()
    {
        recoveryCoroutine = null;
        uprightRotation = mobRenderer.transform.localRotation;
    }

    // Update is called once per frameI'm 
    void Update()
    {  
        // ai brain
        // this will be turned into a seperate script later down the line
        MobLogic();
    }

    public void SetTarget(GameObject target)                                                                                                                                                                                                                                               
    {                                                                                                                                                                                                                                                                                      
         protagObject = target;                                                                                                                                                                                                                                                             
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
            int index = (int) face;
            mobAnimator.SetInteger("Facing", index);
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

    void MobLogic()
    {
        // note that the current AI simply just moves towards the player
        // it doesn't care at all about obstacles, walls or whether it should
        // see the player.
        // in the future this function should be its own class but for the
        // purposes of this checkoff it's acceptable.
        Vector3 toTarget = protagObject.transform.position - transform.position; // constructs the vector from player to mouse
        if(!isStunned)
        {
            MoveBody(toTarget);
            ProcessFacing(toTarget);
        }
    }
    void MoveBody(Vector3 toTarget)
    {
        mobBody.linearVelocity = toTarget.normalized * moveSpeed;

        // TODO: use this for nograv environments
        // medium priority, also this implementation makes the player slow as hell
        // distanceThisFrame = moveSpeed * Time.deltaTime;
        // protagBody.AddForce(step2D.normalized * distanceThisFrame);
    }

    public void OnShove(Vector2 impulseVector, float shoveDuration)
    {
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
    void FixedUpdate()
    {
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
