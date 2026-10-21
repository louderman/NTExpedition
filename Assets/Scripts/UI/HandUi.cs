using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Runtime.ExceptionServices;

public class HandUI : MonoBehaviour
{
    [SerializeField] private Image leftHighlight;
    [SerializeField] private Image rightHighlight;
    [SerializeField] private HandSystem handSystem;
    
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
        // forcing an update on  this fuckass script
    }

    void OnEnable()
    {
        handSystem.HandChanged += DisplayHand;
        DisplayHand(handSystem.CurrentHandSlot);
    }

    void OnDisable()
    {
        handSystem.HandChanged -= DisplayHand;
    }

    public void DisplayHand(HandSystem.HandSlot slot)
    {
        if(slot == HandSystem.HandSlot.Left)
        {
            leftHighlight.enabled = true;
            rightHighlight.enabled = false;
        }
        else
        {
            rightHighlight.enabled = true;
            leftHighlight.enabled = false;
        }
    }

}