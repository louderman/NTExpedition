using System;
using UnityEngine;

// Only run score persists between scenes. HUD, players, audio and spawners stay scene-local.
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }
    [SerializeField] private ExpeditionConfig config;
    private int score;
    public int CurrentScore => score;
    public int BestScore { get; private set; }
    public event Action<int> ScoreChanged;
    public event Action<int> BestScoreChanged;
    private string SaveKey => config != null ? config.highscoreKey : "NTExpedition.BestScore.v1";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStatics() { Instance = null; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (config == null) config = Resources.Load<ExpeditionConfig>("ExpeditionConfig");
        score = config != null ? config.startingScore : 0;
        BestScore = PlayerPrefs.GetInt(SaveKey, 0);
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);
    }

    public void AddScore(int amount)
    {
        if (amount <= 0) return;
        score += amount;
        if (score > BestScore)
        {
            BestScore = score;
            PlayerPrefs.SetInt(SaveKey, BestScore);
            PlayerPrefs.Save(); // Real disk persistence, including standalone builds.
            BestScoreChanged?.Invoke(BestScore);
        }
        ScoreChanged?.Invoke(score);
    }

    public void ResetCurrentScore()
    {
        score = config != null ? config.startingScore : 0;
        ScoreChanged?.Invoke(score);
    }

    public void ResetBestScore()
    {
        BestScore = 0;
        PlayerPrefs.DeleteKey(SaveKey); // Never delete unrelated preferences.
        PlayerPrefs.Save();
        BestScoreChanged?.Invoke(BestScore);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}
