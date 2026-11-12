using UnityEngine;

// A State asset describes what happens while the controller is in it.
// Setup/exit actions run exactly once per entry/exit; regular actions run every frame;
// event-triggered actions run only when the state is asked to cast them (e.g. Attack input).
[CreateAssetMenu(menuName = "Lab5/PluggableSM/State")]
public class State : ScriptableObject
{
    public StateAction[] setupActions;
    public StateAction[] actions;
    public EventAction[] eventTriggeredActions;
    public StateAction[] exitActions;
    public Transition[] transitions;

    // Visual aid while a StateController is selected in the Scene view.
    public Color sceneGizmoColor = Color.grey;

    /********************************/
    /* REGULAR METHODS */
    public void UpdateState(StateController controller)
    {
        DoActions(controller);
        CheckTransitions(controller);
    }

    protected void DoActions(StateController controller)
    {
        if (actions == null) return;
        for (int i = 0; i < actions.Length; i++)
            if (actions[i] != null) actions[i].Act(controller);
    }

    public void DoSetupActions(StateController controller)
    {
        if (setupActions == null) return;
        for (int i = 0; i < setupActions.Length; i++)
            if (setupActions[i] != null) setupActions[i].Act(controller);
    }

    public void DoExitActions(StateController controller)
    {
        if (exitActions == null) return;
        for (int i = 0; i < exitActions.Length; i++)
            if (exitActions[i] != null) exitActions[i].Act(controller);
    }

    public void DoEventTriggeredActions(StateController controller, ActionType type = ActionType.Default)
    {
        if (eventTriggeredActions == null) return;
        foreach (EventAction eventTriggeredAction in eventTriggeredActions)
        {
            if (eventTriggeredAction.type != type || eventTriggeredAction.action == null) continue;
            eventTriggeredAction.action.Act(controller);
        }
    }

    protected void CheckTransitions(StateController controller)
    {
        controller.transitionStateChanged = false; // reset
        if (transitions == null) return;
        for (int i = 0; i < transitions.Length; ++i)
        {
            // Stop as soon as a transition changed state; the controller's next Update runs the new state.
            if (controller.transitionStateChanged) break;
            Transition transition = transitions[i];
            if (transition == null || transition.decision == null) continue;
            bool decisionSucceeded = transition.decision.Decide(controller);
            controller.TransitionToState(decisionSucceeded ? transition.trueState : transition.falseState);
        }
    }
    /********************************/
}
