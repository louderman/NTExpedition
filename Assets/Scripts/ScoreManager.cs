using System;
using UnityEngine;

// AI-assisted cross-scene demonstration: stores score, never a scene's text component.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }
    private int score;
    public int CurrentScore => score;
    public event Action<int> ScoreChanged;

    void Awake()
    {
        // A new scene also contains a manager so it can be played directly.
        // Keep the original instance and discard only the new duplicate.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this; // Singleton access/uniqueness is separate from persistence below.
        DontDestroyOnLoad(gameObject); // This root survives; scene HUDs do not.
    }

    public void AddScore(int amount)
    {
        score += amount;
        ScoreChanged?.Invoke(score); // Notify the currently enabled scene HUD.
    }

    void OnDestroy()
    {
        // Destroying a duplicate must not clear the surviving instance.
        if (Instance == this) Instance = null;
    }
}
