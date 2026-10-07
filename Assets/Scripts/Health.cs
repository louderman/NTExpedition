using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    [SerializeField] private int maxHP;
    private float currentHP;
    public int MaxHP {get {return maxHP;}}
    public float CurrentHP {get {return currentHP;}}
    public event Action<float, int> HealthChanged;
    public event Action Died;
    
    void Start()
    {
        currentHP = maxHP;
    }

    // Update is called once per frame
    void Update()
    {
        // forcing an update on  this fuckass script
    }

    public void TakeDamage(float amount)
    {
        if(CurrentHP <= 0)
        {
            return;
        }
        if(amount <= 0)
        {
            return;
        }
        currentHP -= amount;
        currentHP = Mathf.Clamp(CurrentHP, 0f, MaxHP);
        Debug.Log($"HP: {currentHP}");
        HealthChanged?.Invoke(currentHP, maxHP);
        if(CurrentHP == 0)
        {
            Died?.Invoke();
            Debug.Log("YOU DIED");
        }
    }
}