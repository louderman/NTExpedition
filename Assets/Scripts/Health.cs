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
    [SerializeField] private bool debugDamageEnabled = false;

    
    void Awake()
    {
        currentHP = maxHP;
        HealthChanged?.Invoke(currentHP, maxHP);
    }

    void Update()
    {
        if (!debugDamageEnabled) return;

        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) return;
        
        // One press of F8 deals 10 damage; holding it does not repeat damage.
        if (keyboard.f8Key.wasPressedThisFrame)
            TakeDamage(10f);
    }

    public void Heal(float amount)
    {
        if (currentHP <= 0f || amount <= 0f) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        HealthChanged?.Invoke(currentHP, maxHP);
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