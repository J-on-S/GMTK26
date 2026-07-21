using UnityEngine.Audio;
using UnityEngine;
using System;
using Unity.VisualScripting;

[CreateAssetMenu(fileName = "AudioClip")]
public class Audio : ScriptableObject
{
    public AudioClip AudioClip;

    [Range(0, 1)]
    public float Volume;

    public bool Loop;
    [Range(-1, 1)]
    public float Pan;

    [Range(0f, 3f)]
    public float Pitch = 1f;

    [Range(0f, 1f)]
    [Tooltip("Max random pitch offset applied on play: pitch +- PitchVariation at random.")]
    public float PitchVariation = 0f;

    /// <summary>
    /// Pitch with a random offset in [-PitchVariation, +PitchVariation] applied.
    /// </summary>
    public float GetRandomizedPitch() =>
        Pitch + UnityEngine.Random.Range(-PitchVariation, PitchVariation);
}
