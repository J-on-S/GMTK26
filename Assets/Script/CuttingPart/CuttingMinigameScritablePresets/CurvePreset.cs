using UnityEngine;

[CreateAssetMenu(fileName = "CurvePreset", menuName = "CurvePreset")]
public class CurvePreset : ScriptableObject
{
    [Tooltip("Warp the flat cut into a wavy ring: each loop point is pushed up/down the body axis by a sine of its angle around the ring. 0 = flat. Raise it and the drawn guide loop rides up and down the surface, so the cursor must track a moving target.")]
    public float curveAmplitude = 0f;

    [Tooltip("Number of full up/down waves around the ring. 1 = a single tilt (one high side, one low). Higher = more, tighter humps.")]
    public float curveWaves = 2f;

    public float curvePhase = 0;

    [Tooltip("Break the clean sine: each half-cycle around the ring gets a random height and width, so the curve is bumpy and irregular instead of a pure wave. Stable per seed, so it stays learnable.")]
    public bool curveRandom = false;

    [Tooltip("Seed for the random curve. Change it to reshuffle the bumps into a new fixed shape.")]
    public int curveSeed = 0;
}
