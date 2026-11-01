using UnityEngine;

// A Decision answers one question about the controller; the answer picks the transition target.
public abstract class Decision : ScriptableObject
{
    public abstract bool Decide(StateController controller);
}

// A Transition is a decision plus the state to enter for the true and false answers.
[System.Serializable]
public class Transition
{
    public Decision decision;
    public State trueState;
    public State falseState;
}
