using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.Serialization;
using UnityEngine;

// note, eventually I want to add a player detection system as well.
// for now we'll just have mobs that follow the player around.

public class MobMovement : MonoBehaviour
{
    public float moveSpeed;
    private float distanceThisFrame;
    private int x;
    private int y;
    private Boolean isStunned;
    [SerializeField] private SpriteRenderer mobRenderer;                                                                                                                                          
    [SerializeField] private Sprite[] mobSprites;
    [SerializeField] private GameObject protagObject;
    [SerializeField] private Animator mobAnimator;
    [SerializeField] private Rigidbody2D mobBody;

    private enum Facing { South, North, East, West };
    private List<Facing> faceList = new();
    void Start()
    {
    }

    // Update is called once per frameI'm 
    void Update()
    {  
        // ai brain
        // this will be turned into a seperate script later down the line
        MobLogic();
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
            Debug.Log("Animator is found");
            int index = (int) face;
            mobAnimator.SetInteger("Facing", index);
        }
        else
        {
            // fallback to static sprite handling
            int index = (int) face;
            mobRenderer.sprite = mobSprites[index];
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
        isStunned = true;
        mobBody.AddForce(impulseVector, ForceMode2D.Impulse);
        StartCoroutine(Stunned(shoveDuration));
    }

    IEnumerator Stunned(float time)
    {
        yield return new WaitForSeconds(time);
        isStunned = false;
    }
    void FixedUpdate()
    {
    }
}
