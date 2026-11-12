using UnityEngine;

// Lab 5 powerup data asset. Each concrete powerup owns its own effect and asks the target to apply
// it, so no other script needs to know what a powerup does (handout: the Magic Mushroom decides to
// call MakeSuperMario; Mario only decides what "super" means).
//
// The concrete powerups live in their own files so Unity can create a MonoScript (and therefore an
// inspector asset) for each one.
public interface IPowerup
{
    bool CanApply(MonoBehaviour target);
    void ApplyPowerup(MonoBehaviour target);
}

public abstract class PowerupDefinition : ScriptableObject, IPowerup
{
    [SerializeField] private string displayName = "Powerup";
    [SerializeField] private PowerupType powerupType = PowerupType.Default;
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private AudioClip collectSound;

    public string DisplayName => displayName;
    public PowerupType PowerupType => powerupType;
    public Color Tint => tint;
    public AudioClip CollectSound => collectSound;

    public virtual bool CanApply(MonoBehaviour target) => target != null;

    public abstract void ApplyPowerup(MonoBehaviour target);

    // Editor-time authoring helper used by the Lab 5 setup script.
    public void Author(string name, PowerupType type, Color color, AudioClip clip)
    {
        displayName = name;
        powerupType = type;
        tint = color;
        collectSound = clip;
    }
}
