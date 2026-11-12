using UnityEngine;

// Lab 5: the component that drives a State asset graph.
// Subclass this per actor (ExpeditionerStateController, BuffStateController).
// Do not modify TransitionToState / CheckIfCountDownElapsed / Update; override the virtual hooks.
public abstract class StateController : MonoBehaviour
{
    public State startState;
    public State previousState;
    public State currentState;
    public State remainState;
    public bool transitionStateChanged = false;
    [HideInInspector] public float stateTimeElapsed;

    public bool isActive = true;

    private SfxPlayer sfx;
    private SpriteFlicker flicker;

    public SfxPlayer Sfx
    {
        get
        {
            if (sfx == null) sfx = GetComponent<SfxPlayer>();
            return sfx;
        }
    }

    public SpriteFlicker Flicker
    {
        get
        {
            if (flicker == null) flicker = GetComponent<SpriteFlicker>();
            return flicker;
        }
    }

    public virtual void Start()
    {
        OnSetupState(); // setup when the scene starts
    }

    public virtual void OnSetupState()
    {
        if (currentState) currentState.DoSetupActions(this);
    }

    public virtual void OnExitState()
    {
        stateTimeElapsed = 0; // reset time in this state
        if (currentState) currentState.DoExitActions(this);
    }

    // Visual aid showing which state this object is in.
    public virtual void OnDrawGizmos()
    {
        if (currentState == null) return;
        Gizmos.color = currentState.sceneGizmoColor;
        Gizmos.DrawWireSphere(transform.position, 1.0f);
    }

    /********************************/
    /* Regular methods: strictly transitions, no gameplay action here. */
    public void TransitionToState(State nextState)
    {
        if (nextState == null || nextState == remainState) return;

        OnExitState(); // exit actions of the state we are leaving

        previousState = currentState;
        currentState = nextState;
        transitionStateChanged = true;

        OnSetupState(); // setup actions of the state we are entering
    }

    // True once the controller has spent `duration` seconds in the current state.
    // Assumes it is called once per Update frame (Time.deltaTime).
    public bool CheckIfCountDownElapsed(float duration)
    {
        stateTimeElapsed += Time.deltaTime;
        return stateTimeElapsed >= duration;
    }

    public void Update()
    {
        if (!isActive || currentState == null) return;
        currentState.UpdateState(this);
    }
    /********************************/

    // Shared helpers so one PlaySound/Invincibility action asset works for either state machine.
    public void PlayStateSound(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;
        if (Sfx != null) Sfx.Play(clip, volume);
    }

    public void StartFlicker(float interval = 0.12f)
    {
        if (Flicker != null) Flicker.Request(interval);
    }

    public void StopFlicker()
    {
        if (Flicker != null) Flicker.Release();
    }
}
