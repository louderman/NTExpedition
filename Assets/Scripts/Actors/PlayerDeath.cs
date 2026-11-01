using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Game-over presentation. It no longer subscribes to Health directly: the actor state machine
// raises OnPlayerDied, and a SimpleGameEventListener on this GameObject calls HandleDeath.
// A single game-over path avoids the old duplicate-death handling.
public class PlayerDeath : MonoBehaviour
{
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private IntVariable runScore;

    public void HandleDeath()
    {
        var flow = FindAnyObjectByType<ExpeditionFlow>();
        if (flow != null) { flow.EndRun(); return; }

        // Fallback for a scene without the flow object (keeps the existing panel usable).
        if (finalScoreText != null) finalScoreText.text = $"Score: {(runScore != null ? runScore.Value : 0)}";
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void RestartGame()
    {
        var flow = FindAnyObjectByType<ExpeditionFlow>();
        if (flow != null) { flow.RestartRun(); return; }
        if (runScore != null) runScore.ResetToDefault();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
