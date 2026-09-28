using System;
using UnityEngine;
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
    private enum Facing { South, North, East, West };
    private Facing lockedFacing = Facing.South;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {  
        // conditional: if key is pressed ? 1 if true, 0 if false
        // a little bit hard to read but this compresses like 24 lines worth of if statements so its pretty good
        x = (Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0);
        y = (Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0);
        MoveBody(x, y);

        // normalize resultant vector. does nothing if x 1 but y 0 or vice versa, but necessary
        // when x and y are both 1 as a diagonal vector of (1, 1) is 1.5x faster than a regular horizontal/vertical movement
        // transform.position += step.normalized * distanceThisFrame;

        // now for sprite facing
        // TODO: compress this, low priority
        if (Keyboard.current.wKey.wasPressedThisFrame == true)
        { 
            lockedFacing = Facing.North;
            SetFacing(Facing.North);
        }
        if (Keyboard.current.dKey.wasPressedThisFrame == true)
        { 
            lockedFacing = Facing.East;
            SetFacing(Facing.East);
        }
        if (Keyboard.current.sKey.wasPressedThisFrame == true)
        { 
            lockedFacing = Facing.South;
            SetFacing(Facing.South);
        }
        if (Keyboard.current.aKey.wasPressedThisFrame == true)
        { 
            lockedFacing = Facing.West;
            SetFacing(Facing.West);
        }
    }

    void SetFacing(Facing face)
    {
        int index = (int)face;
        suitRenderer.sprite = suitSprites[index];
        helmetRenderer.sprite = helmetSprites[index];
    }

    void MoveBody(int x, int y)
    {
        step2D = new Vector2(x, y);
        protagBody.linearVelocity = step2D.normalized * moveSpeed;

        // TODO: use this for nograv environments
        // medium priority, also this implementation makes the player slow as hell
        // distanceThisFrame = moveSpeed * Time.deltaTime;
        // protagBody.AddForce(step2D.normalized * distanceThisFrame);
    }
    void FixedUpdate()
    {
    }
}
