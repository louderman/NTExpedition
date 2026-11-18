using UnityEngine;

// The buff state machine's equivalent of TransformDecision: compares the current buff state name
// against BuffState and the requested BuffType.
[CreateAssetMenu(menuName = "Lab5/PluggableSM/Decisions/BuffTransform")]
public class BuffTransformDecision : Decision
{
    public BuffTransformMap[] map;

    public override bool Decide(StateController controller)
    {
        BuffStateController buffs = controller as BuffStateController;
        if (buffs == null || map == null) return false;
        BuffState current = EnumExtension.ParseEnum<BuffState>(controller.currentState.name);
        for (int i = 0; i < map.Length; i++)
            if (current == map[i].fromState && buffs.currentBuffType == map[i].buffType)
                return true;
        return false;
    }
}
