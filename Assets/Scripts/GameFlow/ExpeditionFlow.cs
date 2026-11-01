using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Scene flow for one screen. This is a scene object, not a singleton and not DontDestroyOnLoad:
// every persistent value now lives in a ScriptableObject variable, so a fresh flow object in the
// next scene picks the values up instead of carrying an object across scenes.
public class ExpeditionFlow : MonoBehaviour
{
    public enum Screen { Menu, Loading, World }
    public Screen screen;
    public ExpeditionConfig config;
    public AudioSource musicSource;
    public AudioSource effectsSource;
    public TMP_Text statusText;
    public GameObject pausePanel;
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;
    public PlayerMovement player;

    [Header("Lab 5 ScriptableObject runtime data")]
    [SerializeField] private IntVariable runScore;
    [SerializeField] private IntVariable bestScore;
    [SerializeField] private SimpleGameEvent onGameRestart;

    public bool IsPaused { get; private set; }
    public bool IsGameOver { get; private set; }

    private bool navigating;
    private PlayerInput playerInput;
    private BuffStateController buffs;

    private string SaveKey => config != null ? config.highscoreKey : "NTExpedition.BestScore.v1";

    void Start()
    {
        Time.timeScale = 1f;
        if (player != null)
        {
            playerInput = player.GetComponent<PlayerInput>();
            buffs = player.GetComponent<BuffStateController>();
        }
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (musicSource != null && config != null && config.music != null) musicSource.Play();
        if (bestScore != null) bestScore.SetValue(PlayerPrefs.GetInt(SaveKey, 0));
        if (runScore != null) runScore.ValueChanged += OnRunScoreChanged;
    }

    void OnDestroy()
    {
        if (runScore != null) runScore.ValueChanged -= OnRunScoreChanged;
    }

    void Update()
    {
        if (screen != Screen.World) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !IsGameOver)
            TogglePause();
        if (statusText != null && player != null)
        {
            string buffLine = "Suit ready";
            if (buffs != null)
            {
                if (buffs.IsInvincible) buffLine = $"Shield: {buffs.RemainingSeconds:0.0}s";
                else if (buffs.IsOvercharged) buffLine = $"Suit overcharge: {buffs.RemainingSeconds:0.0}s";
            }
            statusText.text = SceneManager.GetActiveScene().name + "  |  " + buffLine +
                "\nWASD move  ·  1 combat  ·  Left click punch  ·  Right click shove  ·  Z powerup attack  ·  E supply station  ·  Esc pause";
        }
    }

    // Real disk persistence for the best score only; the current run score is ScriptableObject data.
    void OnRunScoreChanged(int value)
    {
        if (bestScore == null || value <= bestScore.Value) return;
        bestScore.SetValue(value); // raises the OnBestScoreChanged channel through the variable
        PlayerPrefs.SetInt(SaveKey, value);
        PlayerPrefs.Save();
    }

    public void Play()
    {
        if (navigating) return;
        if (runScore != null) runScore.ResetToDefault();
        Navigate(config.loadingScene);
    }

    public void ContinueToWorld() { Navigate(config.firstWorld); }
    public void ReturnToMenu() { Navigate(config.menuScene); }

    public void ResetBestScore()
    {
        if (bestScore != null) bestScore.SetValue(0);
        PlayerPrefs.DeleteKey(SaveKey); // never deletes unrelated preferences
        PlayerPrefs.Save();
        EventSystem.current?.SetSelectedGameObject(null);
    }

    public void Quit() { Application.Quit(); }

    public void RestartRun()
    {
        if (navigating) return;
        // Every listener of OnGameRestart resets its own component (actor FSM, buff FSM, camera,
        // run score, audio), so restarting the game returns the whole level to its authored start
        // state without any manager object holding the values.
        if (onGameRestart != null) onGameRestart.Raise();
        Navigate(config.firstWorld);
    }

    // Response for the OnGameRestart SimpleGameEventListener on this object: the run score is
    // ScriptableObject runtime data, so it is reset by the same shared channel that resets the
    // actor, the buffs and the camera (a scene reload alone would leave it carrying the old run).
    public void ResetRunScore()
    {
        if (runScore != null) runScore.ResetToDefault();
    }

    // Response for the OnPlayerDied SimpleGameEventListener.
    public void HandlePlayerDied() { EndRun(); }

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
        PlayEffect(config != null ? config.gameOverSound : null);
        int score = runScore != null ? runScore.Value : 0;
        int best = bestScore != null ? bestScore.Value : 0;
        if (gameOverText != null) gameOverText.text = $"Expedition ended\nScore: {score}  ·  Best: {best}";
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
