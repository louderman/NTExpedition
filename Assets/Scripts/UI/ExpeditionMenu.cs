using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Shared, Inspector-authored presentation. ExpeditionFlow owns the run and pause state.
public class ExpeditionMenu : MonoBehaviour
{
    [Serializable]
    public class Page
    {
        public CanvasGroup group;
        public Selectable firstSelection;
        public void Show(bool visible) => group.gameObject.SetActive(visible);
        public void EnableInput(bool enabled)
        {
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }
    }

    [Header("Screens")]
    [SerializeField] private Page mainMenu;
    [SerializeField] private Page briefing;
    [SerializeField] private Page gameplay;
    [SerializeField] private Page pause;
    [SerializeField] private Page gameOver;
    [Header("Modals")]
    [SerializeField] private Page instructions;
    [SerializeField] private Page credits;
    [SerializeField] private Page records;
    [SerializeField] private Page confirmation;
    [SerializeField] private Page loading;
    [Header("Live content")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text confirmationTitle;
    [SerializeField] private TMP_Text confirmationText;
    [SerializeField] private TMP_Text confirmationAction;
    [SerializeField] private ScrollRect instructionsScroll;
    [SerializeField] private RectTransform loadingIndicator;

    private readonly Stack<(Page page, GameObject selection)> modals = new();
    private static readonly List<RaycastResult> pointerHits = new();
    private ExpeditionFlow flow;
    private Page current;
    private Action confirmedAction;
    private bool isLoading;

    public void Initialize(ExpeditionFlow owner)
    {
        flow = owner;
        foreach (var page in new[] { mainMenu, briefing, gameplay, pause, gameOver,
                     instructions, credits, records, confirmation, loading })
            page.Show(false);
        current = flow.screen == ExpeditionFlow.Screen.Menu ? mainMenu :
            flow.screen == ExpeditionFlow.Screen.Loading ? briefing : gameplay;
        current.Show(true);
        current.EnableInput(true);
        Select(current == gameplay ? null : current.firstSelection);
    }

    void Update()
    {
        if (isLoading)
            loadingIndicator.anchoredPosition = new Vector2(
                Mathf.PingPong(Time.unscaledTime * 180f, 360f) - 180f, 0f);

        var keyboard = Keyboard.current;
        if (keyboard == null || isLoading) return;
        if (modals.Count > 0 && modals.Peek().page == instructions)
        {
            if (keyboard.pageDownKey.wasPressedThisFrame)
                instructionsScroll.verticalNormalizedPosition = Mathf.Clamp01(instructionsScroll.verticalNormalizedPosition - 0.4f);
            if (keyboard.pageUpKey.wasPressedThisFrame)
                instructionsScroll.verticalNormalizedPosition = Mathf.Clamp01(instructionsScroll.verticalNormalizedPosition + 0.4f);
        }
        // uGUI handles arrows and submit. Tab also cycles within the active screen/modal.
        if (keyboard.tabKey.wasPressedThisFrame)
            CycleSelection(keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var active = modals.Count > 0 ? modals.Peek().page : current;
        if ((selected == null && active != gameplay) ||
            (selected != null && !selected.transform.IsChildOf(active.group.transform)))
            Select(active.firstSelection);
    }

    void CycleSelection(bool backwards)
    {
        var page = modals.Count > 0 ? modals.Peek().page : current;
        var choices = page.group.GetComponentsInChildren<Selectable>();
        var available = Array.FindAll(choices, item => item.IsActive() && item.IsInteractable());
        if (available.Length == 0) return;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        int index = Array.FindIndex(available, item => item.gameObject == selected);
        index = index < 0 ? (backwards ? available.Length - 1 : 0) :
            (index + (backwards ? available.Length - 1 : 1)) % available.Length;
        Select(available[index]);
    }

    static void Select(Selectable target)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target != null ? target.gameObject : null);
    }

    void Open(Page page)
    {
        if (isLoading || (modals.Count > 0 && modals.Peek().page == page)) return;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        (modals.Count > 0 ? modals.Peek().page : current).EnableInput(false);
        modals.Push((page, selected));
        page.Show(true);
        page.EnableInput(true);
        flow.SetModalOpen(true);
        Select(page.firstSelection);
    }

    public void CloseModal()
    {
        if (isLoading || modals.Count == 0) return;
        var closed = modals.Pop();
        if (closed.page == confirmation) confirmedAction = null;
        closed.page.Show(false);
        var active = modals.Count > 0 ? modals.Peek().page : current;
        active.EnableInput(true);
        flow.SetModalOpen(modals.Count > 0);
        if (closed.selection != null && closed.selection.activeInHierarchy && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(closed.selection);
        else Select(active.firstSelection);
    }

    public bool HandleEscape()
    {
        if (isLoading) return true;
        if (modals.Count == 0) return false;
        CloseModal();
        return true;
    }

    public void ShowInstructions()
    {
        Open(instructions);
        Canvas.ForceUpdateCanvases();
        instructionsScroll.verticalNormalizedPosition = 1f;
    }

    public void ShowCredits() => Open(credits);
    public void ShowRecords() => Open(records);
    public void Play() => flow.Play();
    public void Continue() => flow.ContinueToWorld();
    public void BackToMenu() => flow.ReturnToMenu();
    public void TogglePause() => flow.TogglePause();
    public void SetStatus(string text) => statusText.text = text;

    public void ShowPause(bool paused)
    {
        gameplay.Show(!paused);
        pause.Show(paused);
        current = paused ? pause : gameplay;
        current.EnableInput(true);
        Select(paused ? pause.firstSelection : null);
    }

    public void ShowGameOver(int score, int best)
    {
        ClearModals();
        current.Show(false);
        gameplay.Show(false);
        pause.Show(false);
        current = gameOver;
        resultText.text = $"Score  {score}\nBest score  {best}";
        gameOver.Show(true);
        gameOver.EnableInput(true);
        Select(gameOver.firstSelection);
    }

    public void ShowLoading()
    {
        isLoading = true;
        ClearModals();
        current.EnableInput(false);
        loading.Show(true);
        loading.EnableInput(true);
        Select(null);
        // Present at least one layout pass before the asynchronous operation begins.
        Canvas.ForceUpdateCanvases();
    }

    void ClearModals()
    {
        while (modals.Count > 0) modals.Pop().page.Show(false);
        flow.SetModalOpen(false);
        confirmedAction = null;
    }

    void Confirm(string title, string message, string actionLabel, Action action)
    {
        if (isLoading) return;
        confirmationTitle.text = title;
        confirmationText.text = message;
        confirmationAction.text = actionLabel;
        confirmedAction = action;
        Open(confirmation); // Cancel is the prefab's initial selection.
    }

    public void ConfirmAction()
    {
        var action = confirmedAction;
        confirmedAction = null;
        CloseModal();
        action?.Invoke();
    }

    public void RequestQuit() => Confirm("QUIT GAME?",
        flow.screen == ExpeditionFlow.Screen.World && !flow.IsGameOver
            ? "Your current expedition will be lost." : "Close Wreckbound?",
        "Quit", flow.Quit);

    public void RequestRestart()
    {
        if (flow.IsGameOver) { flow.RestartRun(); return; }
        Confirm("RESTART EXPEDITION?", "Your current expedition will be lost.", "Restart", flow.RestartRun);
    }

    public void RequestMainMenu()
    {
        if (flow.IsGameOver) { flow.ReturnToMenu(); return; }
        Confirm("LEAVE EXPEDITION?", "Your current expedition will be lost.", "Main menu", flow.ReturnToMenu);
    }

    public void RequestResetBest() => Confirm("RESET BEST SCORE?",
        "This erases your saved best score. It cannot be undone.", "Reset", flow.ResetBestScore);

    // Raycast now: Input System callbacks can run before uGUI updates its pointer cache.
    public static bool PointerOverInterface()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;
        var pointer = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
        pointerHits.Clear();
        EventSystem.current.RaycastAll(pointer, pointerHits);
        return pointerHits.Exists(hit => hit.module is GraphicRaycaster);
    }
}
