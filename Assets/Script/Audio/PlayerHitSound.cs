using System.Collections.Generic;
using UnityEngine;


public class PlayerHitSound : MonoBehaviour, AudioMaker
{
    [Tooltip("Optional. An Audio Set here replaces the list below: the asset does the picking, so the same sounds can be shared by every object that needs them.")]
    [SerializeField] private Audio soundSet;

    [SerializeField] private List<Audio> hitSounds = new List<Audio>();

    [SerializeField] private AudioEventChannel channel;

    [SerializeField] private AudioPick decider = AudioPick.Random;
    private int index = 0;

    /// <summary>Whether an asset is doing the picking instead of the inline list.</summary>
    public bool UsesAudioSet => soundSet != null;

    public void playAudio()
    {
        if (channel == null)
        {
            Debug.LogError("AudioEventChannel channel is not assigned in playerHitSound : " + gameObject.name);
            return;
        }

        // An AudioSet is an Audio, so it can be handed straight over: the AudioMaster asks it for a
        // variant when it starts playing. Nothing here needs to know which one it will be.
        if (soundSet != null)
        {
            channel.Play(soundSet);
            return;
        }

        if (hitSounds == null || hitSounds.Count == 0)
        {
            Debug.LogWarning("No sounds to play in playerHitSound : " + gameObject.name, this);
            return;
        }

        Audio s;

        if (decider == AudioPick.Random)
        {
            s = GetRandomSound();
        }
        else
        {
            s = hitSounds[index % hitSounds.Count];
            index++;
        }

        channel.Play(s);
    }

    Audio GetRandomSound()
    {
        if (hitSounds == null || hitSounds.Count == 0)
            return null;

        int i = Random.Range(0, hitSounds.Count);
        return hitSounds[i];
    }
}
