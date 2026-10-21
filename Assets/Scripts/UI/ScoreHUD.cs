using TMPro;
using UnityEngine;

// AI-assisted: this component and its text belong to the scene, not the persistent manager.
public class ScoreHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;
    private ScoreManager manager;

    void Start()
    {
        // Start follows scene Awake calls, so the singleton has been established.
        BindAndRender();
    }

    void OnEnable()
    {
        // On first enable the manager may not have run Awake yet; Start handles that.
        // On later enables this also refreshes any changes missed while disabled.
        if (ScoreManager.Instance != null) BindAndRender();
    }

    void BindAndRender()
    {
        if (manager != null) manager.ScoreChanged -= DisplayScore;
        manager = ScoreManager.Instance;
        if (manager == null || scoreText == null)
        {
            Debug.LogError("ScoreHUD needs a ScoreManager and an assigned scene text.", this);
            return;
        }
        manager.ScoreChanged += DisplayScore; // Register a callback; this does not render.
        DisplayScore(manager.CurrentScore); // Explicitly render existing state immediately.
    }

    void OnDisable()
    {
        if (manager != null) manager.ScoreChanged -= DisplayScore;
        manager = null; // Do not leave the destroyed scene's HUD registered.
    }

    void DisplayScore(int score)
    {
        scoreText.text = $"Score: {score}";
    }
}
