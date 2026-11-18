using UnityEngine;

// Test-only sensor for the executable regression harness. It sits on the same GameObject as the
// firing AudioSource and accumulates the mixed PCM Unity hands to that source's filter chain, which
// proves the imported gunshot clip really reaches the audio mixer instead of merely being assigned to
// a field. Under the headless gamescope run there is no real output device, so the driver reports
// AudioSettings.outputSampleRate 0 and delivers mostly-silent blocks; the non-silent peak captured
// here is the evidence that the shot's audio actually flowed through the mixer.
public class FiringSoundPcmProbe : MonoBehaviour
{
    public int callbackCount { get; private set; }
    public long sampleCount { get; private set; }
    public int sampleRate { get; private set; }
    public float peak { get; private set; }
    public float rms { get; private set; }
    double sumSquares;

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (data == null || data.Length == 0) return;
        for (int i = 0; i < data.Length; i++)
        {
            float sample = data[i];
            float magnitude = sample < 0f ? -sample : sample;
            if (magnitude > peak) peak = magnitude;
            sumSquares += (double)sample * sample;
        }
        sampleCount += data.Length;
        callbackCount++;
        sampleRate = AudioSettings.outputSampleRate;
        rms = sampleCount > 0 ? Mathf.Sqrt((float)(sumSquares / sampleCount)) : 0f;
    }
}
