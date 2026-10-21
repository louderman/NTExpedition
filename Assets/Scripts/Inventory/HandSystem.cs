using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class HandSystem : MonoBehaviour
{
    public enum HandSlot {Left, Right};
    public HandSlot CurrentHandSlot;
    public event System.Action<HandSlot> HandChanged;
    
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SwapHands()
    {
        if(CurrentHandSlot == HandSlot.Left)
        {
            CurrentHandSlot = HandSlot.Right;
        }
        else
        {
            CurrentHandSlot = HandSlot.Left;
        }
        // announce hand change
        HandChanged?.Invoke(CurrentHandSlot);
        Debug.Log($"Hand changed: {CurrentHandSlot}");
    }
}