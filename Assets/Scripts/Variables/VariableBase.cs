using UnityEngine;

// Lab 5: ScriptableObject runtime data. These assets hold authored defaults plus the runtime value
// that survives a scene load, which is how values persist without a DontDestroyOnLoad singleton.
// (Base type only - it carries no serialized fields, so IntVariable/BoolVariable each declare their own.)
public abstract class VariableBase : ScriptableObject
{
}
