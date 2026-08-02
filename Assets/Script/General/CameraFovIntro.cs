using System.Collections;
using UnityEngine;

/// <summary>
/// Sweeps the camera's field of view from <see cref="startFov"/> to whatever it was authored at, over
/// <see cref="duration"/> seconds. The authored value is captured on Start, so the scene view still shows
/// the FOV the shot is framed for.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFovIntro : MonoBehaviour
{
    [Tooltip("Field of view the sweep starts from. Above the camera's own FOV zooms in, below zooms out.")]
    [SerializeField] private float startFov = 90f;

    [Tooltip("Length of the sweep, in seconds.")]
    [SerializeField] private float duration = 2f;

    [Tooltip("Shape of the sweep. Left to right is start to end.")]
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Run on unscaled time, so the sweep still plays while the game is paused or in slow motion.")]
    [SerializeField] private bool unscaledTime = true;

    private Camera cam;
    private Coroutine running;

    /// <summary>The FOV the camera was authored with, which is where the sweep ends.</summary>
    private float targetFov;

    private void Start()
    {
        cam = GetComponent<Camera>();
        targetFov = cam.fieldOfView;
        Play();
    }

    /// <summary>Restarts the sweep from <see cref="startFov"/>.</summary>
    public void Play()
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(Sweep());
    }

    private IEnumerator Sweep()
    {
        if (duration <= 0f)
        {
            cam.fieldOfView = targetFov;
            running = null;
            yield break;
        }

        cam.fieldOfView = startFov;

        float t = 0f;
        while (t < duration)
        {
            t += unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            cam.fieldOfView = Mathf.LerpUnclamped(startFov, targetFov, ease.Evaluate(Mathf.Clamp01(t / duration)));
            yield return null;
        }

        cam.fieldOfView = targetFov;
        running = null;
    }
}
