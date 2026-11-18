using UnityEngine;

// One-shot firing sound, ported from Frontier Station 14's PKA gunshot
// (Gun.soundGunshot: /Audio/Weapons/Guns/Gunshots/kinetic_accel.ogg, declared in
// Resources/Prototypes/Entities/Objects/Weapons/Guns/Basic/base_pka.yml).
//
// Upstream plays the gunshot positionally at the gun. This spawns a temporary 3D source at the
// muzzle with logarithmic rolloff (1 m near, 12 m far) and no doppler, which keeps the shot loud
// next to the camera and quieter across the room; a plain 2D source would throw the muzzle position
// away. The held item's GameObject is inactive while carried, so the source cannot live on the item
// and each shot owns a throwaway source, exactly like overlapping gunshots upstream.
public static class FiringSoundEffect
{
    public const string ObjectName = "Firing sound";
    public const float Volume = 1f;
    public const float MinDistance = 1f;
    public const float MaxDistance = 12f;

    public static AudioSource Play(AudioClip clip, Vector2 position)
    {
        if (clip == null)
        {
            Debug.LogError("Firing sound clip is not configured.");
            return null;
        }

        var sound = new GameObject(ObjectName);
        sound.transform.position = position;
        var source = sound.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Volume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = MinDistance;
        source.maxDistance = MaxDistance;
        source.dopplerLevel = 0f;
        source.playOnAwake = false;
        source.Play();
        Object.Destroy(sound, clip.length + .25f);
        return source;
    }
}
