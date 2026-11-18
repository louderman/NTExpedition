using UnityEngine;

// Small per-actor sound gate. State actions call StateController.PlayStateSound, which forwards here.
[RequireComponent(typeof(AudioSource))]
public class SfxPlayer : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [Range(0f, 1f)] [SerializeField] private float defaultVolume = 1f;

    void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source != null)
        {
            source.playOnAwake = false;
            source.loop = false;
        }
    }

    public void Play(AudioClip clip) => Play(clip, defaultVolume);

    public void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (source == null) source = GetComponent<AudioSource>();
        if (source == null) return;
        source.PlayOneShot(clip, volume);
    }

    public void SetMixerGroup(UnityEngine.Audio.AudioMixerGroup group)
    {
        if (source == null) source = GetComponent<AudioSource>();
        if (source != null) source.outputAudioMixerGroup = group;
    }
}
