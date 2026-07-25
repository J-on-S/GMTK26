using UnityEngine;


 [CreateAssetMenu(fileName = "CameraMovesPreset", menuName = "CameraMovesPreset", order = 0)]
public class CameraMovesPreset : ScriptableObject
{
    

    [Tooltip("Time for the camera to complete one full loop (360 deg) at top speed, in seconds. The speed cap derives from this.")]
    public float secondsPerLoop = 12f;

    /// <summary>Top angular speed in deg/sec: a full 360 loop in secondsPerLoop.</summary>
    public float MaxSpeed => secondsPerLoop > 0f ? 360f / secondsPerLoop : float.MaxValue;

    [Tooltip("Continuous push rate while an arrow key is held (units/sec added to speed).")]
    public float acceleration =4;

    [Tooltip("Speed added per mouse-wheel ridge (one kick, like a skateboard foot push).")]
    public float wheelKick = 3f;

    [Tooltip("Friction rate once coasting ends. Negative = slows down.")]
    public float deceleration = -0.1f;

    [Tooltip("Glide time after the last push before friction starts, in seconds. The board keeps rolling before the foot slows it.")]
    public float coastTime = 0.3f;

    // Direction and backward-input rules live here rather than on the CutSpeedDriver: the driver
    // is shared by every cut, so anything a single cut needs to differ on has to travel with the
    // preset the cut swaps in on entry.

    [Tooltip("Which way this cut travels around the ring: 1 or -1. Scroll and keys are read relative to it, so the player always pushes 'forward'.")]
    public int DirectionMainScroll = 1;

    [Tooltip("Let the player travel backwards along the cut. Off, the speed floor is 0.")]
    public bool canGoBackwards = false;

    [Tooltip("Let input against the travel direction brake: it subtracts speed down to a stop, but never reverses. Ignored when canGoBackwards is on, which already accepts backward input.")]
    public bool canDecelerateManually = false;




}