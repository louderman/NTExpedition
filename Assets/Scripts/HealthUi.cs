using UnityEngine;
using UnityEngine.UI;

public class HealthUi : MonoBehaviour
{
    [SerializeField] Health health;
    [SerializeField] Slider slider;
    
    void OnEnable()
    {
        health.HealthChanged += DisplayHealth;
        DisplayHealth(health.CurrentHP, health.MaxHP);
    }

    void OnDisable()
    {
        health.HealthChanged -= DisplayHealth;
    }

    private void DisplayHealth(float currentHP, int maxHP)
    {
        slider.maxValue = maxHP;
        slider.value = currentHP;
    }
}