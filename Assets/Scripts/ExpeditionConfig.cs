using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(menuName = "NTExpedition/Expedition config")]
public class ExpeditionConfig : ScriptableObject
{
    public string menuScene = "Main Menu";
    public string loadingScene = "Loading";
    public string firstWorld = "HomeShip";
    public int startingScore = 0;
    public string highscoreKey = "NTExpedition.BestScore.v1";
    public float overchargeDuration = 8f;
    public float speedMultiplier = 1.5f;
    public float damageMultiplier = 2f;
    public float repairAmount = 40f;
    public float pickupEmergenceSeconds = 0.45f;
    public AudioClip music;
    public AudioClip spawnSound;
    public AudioClip pickupSound;
    public AudioClip deathSound;
    public AudioClip gameOverSound;
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup effectsGroup;
}
