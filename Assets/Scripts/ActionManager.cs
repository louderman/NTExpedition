using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.InputSystem;

public class ActionManager : MonoBehaviour
{
    [SerializeField] HandSystem handSystem;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
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
