using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
public class ExpeditionFlow : MonoBehaviour
{
    public enum Screen { Menu, Loading, World }
    public Screen screen;
    public ExpeditionConfig config;
    public AudioSource musicSource;
    public AudioSource effectsSource;
    public TMP_Text summaryText;
    public TMP_Text statusText;
    public GameObject pausePanel;
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public PlayerMovement player;
    public bool IsPaused { get; private set; }
    public bool IsGameOver { get; private set; }
    private bool navigating;
    private ScoreManager score;
    private PlayerInput playerInput;

    void Start()
    {
        Time.timeScale = 1f;
        score = ScoreManager.Instance;
        if (score == null) score = new GameObject("ScoreManager").AddComponent<ScoreManager>();
        score.ScoreChanged += RefreshScore;
        score.BestScoreChanged += RefreshScore;
        if (player != null) playerInput = player.GetComponent<PlayerInput>();
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (musicSource != null && config.music != null) musicSource.Play();
        RefreshScore(0);
    }

    void OnDestroy()
    {
        if (score != null) { score.ScoreChanged -= RefreshScore; score.BestScoreChanged -= RefreshScore; }
    }

    void Update()
    {
        if (screen != Screen.World) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !IsGameOver)
            TogglePause();
        if (statusText != null && player != null)
        {
            var buffs = player.GetComponent<PlayerPowerups>();
            statusText.text = SceneManager.GetActiveScene().name + "  |  " +
                (buffs != null && buffs.IsOvercharged ? $"Suit overcharge: {buffs.RemainingSeconds:0.0}s" : "Suit ready") +
                "\nWASD move  ·  1 combat  ·  Left click punch  ·  Right click shove  ·  E supply station  ·  Esc pause";
        }
    }

    void RefreshScore(int unused)
    {
        if (summaryText != null && score != null)
            summaryText.text = screen == Screen.World ? $"Best: {score.BestScore}" : $"Best score: {score.BestScore}";
    }

    public void Play() { if (!navigating) { score.ResetCurrentScore(); Navigate(config.loadingScene); } }
    public void ContinueToWorld() { Navigate(config.firstWorld); }
    public void ReturnToMenu() { Navigate(config.menuScene); }
    public void ResetBestScore() { score.ResetBestScore(); UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null); }
    public void Quit() { Application.Quit(); }

    public void RestartRun()
    {
        if (navigating) return;
        score.ResetCurrentScore();
        // Recreate the first world. All actors, powerups, timers and music are scene-local,
        // and every subsequently visited world is reloaded from its authored initial state.
        Navigate(config.firstWorld);
    }

    public void TogglePause()
    {
        if (screen != Screen.World || IsGameOver || navigating) return;
        IsPaused = !IsPaused;
        Time.timeScale = IsPaused ? 0f : 1f;
        if (pausePanel != null) pausePanel.SetActive(IsPaused);
        if (musicSource != null) { if (IsPaused) musicSource.Pause(); else musicSource.UnPause(); }
        SetPlayerInput(!IsPaused);
    }

    public void EndRun()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        IsPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        if (musicSource != null) musicSource.Stop();
        PlayEffect(config.gameOverSound);
        if (gameOverText != null) gameOverText.text = $"Expedition ended\nScore: {score.CurrentScore}  ·  Best: {score.BestScore}";
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        SetPlayerInput(false);
        Time.timeScale = 0f;
    }

    void SetPlayerInput(bool enabled)
    {
        if (player != null) player.moveVelocity = Vector2.zero;
        if (playerInput != null) { if (enabled) playerInput.ActivateInput(); else playerInput.DeactivateInput(); }
        if (player != null)
            player.GetComponent<WeaponController>()?.Attack(WeaponController.AttackType.PrimaryAttack, WeaponController.AttackPower.CancelCharge);
    }

    public void PlayEffect(AudioClip clip)
    {
        if (effectsSource != null && clip != null) effectsSource.PlayOneShot(clip);
    }

    void Navigate(string scene)
    {
        if (navigating) return;
        if (!Application.CanStreamedLevelBeLoaded(scene)) { Debug.LogError("Missing build scene: " + scene); return; }
        navigating = true;
        Time.timeScale = 1f;
        StartCoroutine(Load(scene));
    }

    IEnumerator Load(string scene)
    {
        // Yield to show the button response before the asynchronous scene load starts.
        yield return null;
        yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
    }
}
