using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


public class PlayerHitSound : MonoBehaviour, SoundMaker
{
    private enum SoundDecider
    {
        RANDOM,
        IN_ORDER
    };

    [SerializeField] private List<Audio> hitSounds = new List<Audio>();

    [SerializeField] private AudioEventChannel channel;

    [SerializeField] private SoundDecider decider = SoundDecider.RANDOM;
    private int index = 0;
    private void Start()
    {
    }

    public void playAudio()
    {
        Audio s;

        if (decider == SoundDecider.RANDOM)
        {
            s = GetRandomSound();
        }
        else
        {
            s = hitSounds[index % hitSounds.Count];
            index++;
        }
        if (channel == null)
        {
            Debug.LogError("AudioEventChannel channel is not assigned in playerHitSound : " + gameObject.name);
            return;
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