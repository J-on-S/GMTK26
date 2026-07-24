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




}