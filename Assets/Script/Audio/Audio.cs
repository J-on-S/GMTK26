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

    [Tooltip("Looping clips only: re-roll the pitch every time the clip wraps, instead of keeping the one picked when it started. Turns a repeating loop into something that doesn't read as the same sample over and over. Needs Loop on and PitchVariation above 0 to do anything.")]
    public bool RandomizePitchEachLoop = false;

    /// <summary>Whether a playing source of this clip should have its pitch re-rolled on each wrap.</summary>
    /// <remarks>All three conditions matter: without <c>Loop</c> there is no wrap, and without <c>PitchVariation</c> every re-roll would return the same number.</remarks>
    public bool WantsPerLoopPitch => Loop && RandomizePitchEachLoop && PitchVariation > 0f;

    /// <summary>
    /// Pitch with a random offset in [-PitchVariation, +PitchVariation] applied.
    /// </summary>
    public float GetRandomizedPitch() =>
        Pitch + UnityEngine.Random.Range(-PitchVariation, PitchVariation);
}
