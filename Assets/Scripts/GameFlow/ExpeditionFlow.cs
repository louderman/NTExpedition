using System.Collections;
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
    [SerializeField] private ExpeditionMenu menu;
    public PlayerMovement player;

    [Header("Lab 5 ScriptableObject runtime data")]
    [SerializeField] private IntVariable runScore;
    [SerializeField] private IntVariable bestScore;
    [SerializeField] private SimpleGameEvent onGameRestart;

    public bool IsPaused { get; private set; }
    public bool IsGameOver { get; private set; }

    private bool navigating;
    private bool modalOpen;
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
        if (musicSource != null && config != null && config.music != null) musicSource.Play();
        if (bestScore != null) bestScore.SetValue(PlayerPrefs.GetInt(SaveKey, 0));
        if (runScore != null) runScore.ValueChanged += OnRunScoreChanged;
        if (menu != null) menu.Initialize(this);
    }

    void OnDestroy()
    {
        if (runScore != null) runScore.ValueChanged -= OnRunScoreChanged;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (menu != null && menu.HandleEscape()) return;
            if (!IsGameOver) TogglePause();
        }
        if (screen != Screen.World) return;
        if (menu != null && player != null)
        {
            string buffLine = "Suit ready";
            if (buffs != null)
            {
                if (buffs.IsInvincible) buffLine = $"Shield: {buffs.RemainingSeconds:0.0}s";
                else if (buffs.IsOvercharged) buffLine = $"Suit overcharge: {buffs.RemainingSeconds:0.0}s";
            }
            menu.SetStatus(buffLine);
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
        if (screen != Screen.World || IsGameOver || navigating || modalOpen) return;
        IsPaused = !IsPaused;
        ApplyInputState();
        if (menu != null) menu.ShowPause(IsPaused);
    }

    public void SetModalOpen(bool open)
    {
        modalOpen = open;
        ApplyInputState();
    }

    void ApplyInputState()
    {
        bool blocked = IsPaused || IsGameOver || modalOpen || navigating;
        Time.timeScale = screen == Screen.World && blocked ? 0f : 1f;
        if (musicSource != null && !IsGameOver)
        {
            if (screen == Screen.World && blocked) musicSource.Pause();
            else musicSource.UnPause();
        }
        SetPlayerInput(!blocked);
    }

    public void EndRun()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        IsPaused = false;
        if (musicSource != null) musicSource.Stop();
        PlayEffect(config != null ? config.gameOverSound : null);
        int score = runScore != null ? runScore.Value : 0;
        int best = bestScore != null ? bestScore.Value : 0;
        if (menu != null) menu.ShowGameOver(score, best);
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

    public void Navigate(string scene)
    {
        if (navigating) return;
        if (!Application.CanStreamedLevelBeLoaded(scene)) { Debug.LogError("Missing build scene: " + scene); return; }
        navigating = true;
        ApplyInputState();
        if (menu != null) menu.ShowLoading();
        StartCoroutine(Load(scene));
    }

    IEnumerator Load(string scene)
    {
        // Yield to show the button response before the asynchronous scene load starts.
        yield return null;
        yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
    }
}
