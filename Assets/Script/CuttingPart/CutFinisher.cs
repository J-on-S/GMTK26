using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Ends a cut with a framed close-up, a waiting tool and one click that chops, so the splice lands under a motion the player made.</summary>
/// <remarks>
/// Invariant: the mesh is sliced on the frame the blade reaches the cut, and the camera is not
/// handed back until the swing and its hold are over.
/// <para>Invariant: the pose maths is side-effect free and takes a clock rather than reading
/// <c>Time.time</c>, so it produces the same poses in edit mode as in play.</para>
/// </remarks>
public class CutFinisher : MonoBehaviour
{
    /// <summary>Point in the swing where the blade reaches the cut and the splice fires.</summary>
    public const float ImpactT = 0.5f;

    [Tooltip("Run the finisher at all. Off, the cut splices and quits the instant progress hits 1.")]
    public bool enableFinisher = true;

    [Tooltip("All of this chop's tuning in one asset. Assign it and every inline number below is ignored.")]
    public CutFinisherPreset preset;

    [Header("Shot")]
    [Tooltip("Whether a close-up has been framed. Off, the camera stays where the cut left it.")]
    public bool hasShot;

    [Tooltip("Where the camera watches the chop from, in the body's local space, so the framing rides the body wherever it is carried.")]
    public Vector3 shotLocalPosition;

    [Tooltip("How the camera is aimed, in the body's local space, in degrees.")]
    public Vector3 shotLocalEuler;

    /// <summary>The cut this finishes, filled from the hierarchy when left empty.</summary>
    public CuttingManager manager;

    // ---- inline tuning: used only while no preset is assigned ----

    [Tooltip("Field of view for the close-up.")]
    public float cameraFOV = 40f;

    [Tooltip("Seconds the camera takes to reach the pose. 0 = snap.")]
    public float easeIn = 0.5f;

    [Tooltip("Shapes the camera's move into the pose.")]
    public AnimationCurve easeInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Object that does the chopping -- bone saw, cleaver, whatever this cut calls for. A scene object is swung where it sits; a prefab asset is spawned. Separate from Required Tool Name, which gates entry rather than choosing what swings.")]
    public GameObject toolPrefab;

    [Tooltip("Extra rotation on the tool, in degrees, on top of the blade-across-the-sweep orientation.")]
    public Vector3 toolEuler;

    [Tooltip("Direction the blade travels across the cut, as an angle around the cut normal in degrees.")]
    [Range(-180f, 180f)]
    public float sweepAngle = 0f;

    [Tooltip("Tilts the approach out of the cutting plane, in degrees. 0 = in-plane sideways chop, 90 = straight down the cut normal.")]
    [Range(-90f, 90f)]
    public float approachTilt = 90f;

    [Tooltip("Bob distance while waiting, in world units, along the approach axis.")]
    public float bobAmp = 0.06f;

    [Tooltip("Bob rate while waiting, in cycles per second.")]
    public float bobHz = 1.5f;

    [Tooltip("Seconds before the swing fires on its own. 0 = wait forever.")]
    public float autoSlashAfter = 0f;

    [Tooltip("How far out along the approach axis the swing starts, in world units. Also where the tool waits.")]
    public float hoverHeight = 0.25f;

    [Tooltip("Half the blade's travel across the cut.")]
    public float sweepDist = 0.6f;

    [Tooltip("Seconds the swing takes.")]
    public float slashTime = 0.18f;

    [Tooltip("Shapes the swing.")]
    public AnimationCurve slashEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Seconds held after the swing lands, before the camera flies back out.")]
    public float holdAfter = 0.25f;

    [Tooltip("Impulse pushing the severed piece away along the approach axis.")]
    public float kick = 3f;

    // ---- resolved tuning: the preset when one is assigned, the inline field otherwise ----

    public float CameraFOV => preset != null ? preset.cameraFOV : cameraFOV;

    /// <summary>Seconds the camera takes to reach the shot.</summary>
    public float EaseIn => preset != null ? preset.easeIn : easeIn;

    public AnimationCurve EaseInCurve => preset != null ? preset.easeInCurve : easeInCurve;

    public GameObject ToolPrefab => preset != null && preset.toolPrefab != null ? preset.toolPrefab : toolPrefab;

    /// <summary>Extra rotation laid on top of the computed blade orientation, in degrees.</summary>
    public Vector3 ToolEuler => preset != null ? preset.toolEuler : toolEuler;

    /// <summary>Angle of the sweep axis around the cut normal, in degrees.</summary>
    public float SweepAngle => preset != null ? preset.sweepAngle : sweepAngle;

    /// <summary>How far the approach leans out of the cutting plane, in degrees.</summary>
    public float ApproachTilt => preset != null ? preset.approachTilt : approachTilt;

    /// <summary>Bob distance while waiting, in world units.</summary>
    public float BobAmp => preset != null ? preset.bobAmp : bobAmp;

    public float BobHz => preset != null ? preset.bobHz : bobHz;

    /// <summary>Seconds before the swing fires on its own; <c>0</c> waits indefinitely.</summary>
    public float AutoSlashAfter => preset != null ? preset.autoSlashAfter : autoSlashAfter;

    /// <summary>How far out along the approach axis the swing starts, and where the tool waits.</summary>
    public float HoverHeight => preset != null ? preset.hoverHeight : hoverHeight;

    /// <summary>Half the blade's travel across the cut, in world units.</summary>
    public float SweepDist => preset != null ? preset.sweepDist : sweepDist;

    /// <summary>Seconds the swing takes.</summary>
    public float SlashTime => preset != null ? preset.slashTime : slashTime;

    public AnimationCurve SlashEase => preset != null ? preset.slashEase : slashEase;

    /// <summary>Seconds held on the aftermath before the camera flies out.</summary>
    public float HoldAfter => preset != null ? preset.holdAfter : holdAfter;

    /// <summary>Impulse pushing the severed piece away; <c>0</c> leaves it where it fell.</summary>
    public float Kick => preset != null ? preset.kick : kick;

    /// <summary>How much world-up is mixed into the severed piece's push, relative to the cut's -normal.</summary>
    /// <remarks>A little, on purpose: enough to lift the piece off the stump so it arcs clear, not so much it flies straight up. ~0.25 is about a 14° lift.</remarks>
    private const float KickUpBias = 0.25f;

    /// <summary>The spawned copy of a prefab-asset tool, or <c>null</c> when none is up. Owned by the finisher and destroyed on release.</summary>
    private GameObject toolInstance;

    /// <summary>Prefab <see cref="toolInstance"/> was made from, so a changed prefab respawns rather than showing the old tool.</summary>
    private GameObject toolInstanceSource;

    /// <summary>A scene object driven in place instead of copied, or <c>null</c>. Belongs to the scene, so it is posed back to rest on release, never destroyed.</summary>
    private GameObject drivenTool;

    /// <summary>The driven tool's pose when the finisher took it, restored on release so the saw does not stay frozen mid-swing.</summary>
    private Vector3 drivenRestPos;
    private Quaternion drivenRestRot;

    /// <summary>The running beat, so a second <see cref="Begin"/> cannot stack two.</summary>
    private Coroutine running;

    /// <summary>The cut this finishes: the explicit reference, else the nearest one up the hierarchy.</summary>
    public CuttingManager Manager
    {
        get
        {
            if (manager == null)
            {
                manager = GetComponentInParent<CuttingManager>();
            }
            return manager;
        }
    }

    /// <summary>The plane this cut runs on, which is the frame the swing is built in.</summary>
    public CutPlane Plane => Manager != null ? Manager.CutPlane : null;

    /// <summary>Whether this finisher has enough to run; <c>false</c> makes the cut splice directly instead.</summary>
    public bool CanRun => enableFinisher && Manager != null && Plane != null;

    // ---- frame basis: an axis to come in along, an axis to sweep across, a centre to aim at ----

    /// <summary>Direction the blade travels across the cut, in world space.</summary>
    /// <remarks>Invariant: unit length, and perpendicular to the cut normal at every <see cref="SweepAngle"/>.</remarks>
    public Vector3 SweepAxis
    {
        get
        {
            CutPlane plane = Plane;
            if (plane == null)
            {
                return Vector3.right;
            }

            float rad = SweepAngle * Mathf.Deg2Rad;
            return plane.transform.right * Mathf.Cos(rad) + plane.transform.forward * Mathf.Sin(rad);
        }
    }

    /// <summary>Direction the tool waits out along and travels back down, in world space.</summary>
    /// <remarks>
    /// Invariant: unit length, and perpendicular to <see cref="SweepAxis"/> at every
    /// <see cref="ApproachTilt"/>.
    /// <para>At a tilt of <c>0</c> the blade stays inside the cutting plane and chops across the
    /// limb; at <c>90</c> it drops down the cut normal, which for a limb runs along it.</para>
    /// </remarks>
    public Vector3 ApproachAxis
    {
        get
        {
            CutPlane plane = Plane;
            if (plane == null)
            {
                return Vector3.up;
            }

            Vector3 normal = plane.Normal;

            // the in-plane axis a quarter turn from the sweep
            Vector3 inPlane = Vector3.Cross(normal, SweepAxis);

            float rad = ApproachTilt * Mathf.Deg2Rad;
            return inPlane * Mathf.Cos(rad) + normal * Mathf.Sin(rad);
        }
    }

    /// <summary>Centre of the cut in world space: the loop's own centre, falling back to the plane origin when the guide has no loop.</summary>
    public Vector3 CutCenter
    {
        get
        {
            LoopGuideBuilder guide = Manager != null ? Manager.loopGuide : null;
            if (guide != null && guide.TryGetCurvedLoop(out Vector3 center, out _))
            {
                return center;
            }

            CutPlane plane = Plane;
            return plane != null ? plane.Origin : transform.position;
        }
    }

    // ---- pure pose maths: no side effects, no Time, callable in edit mode ----

    /// <summary>Space the shot is stored in: the body being cut, falling back to this object while no body is wired.</summary>
    public Transform ShotSpace
    {
        get
        {
            CuttableObject body = Manager != null ? Manager.GameObjectBeingCut : null;
            return body != null ? body.transform : transform;
        }
    }

    /// <summary>Where the camera watches from, resolved into world space through <see cref="ShotSpace"/>.</summary>
    /// <returns><c>false</c> when no shot has been framed, leaving the outputs meaningless.</returns>
    public bool TryGetCameraPose(out Vector3 position, out Quaternion rotation, out float fov)
    {
        fov = CameraFOV;

        position = Vector3.zero;
        rotation = Quaternion.identity;

        Transform space = ShotSpace;
        if (!hasShot || space == null)
        {
            return false;
        }

        // TransformPoint carries the body's scale, so rescaling it after framing keeps the camera
        // the same distance out.
        position = space.TransformPoint(shotLocalPosition);
        rotation = space.rotation * Quaternion.Euler(shotLocalEuler);
        return true;
    }

    /// <summary>Frames the shot from a world pose, storing it relative to the body.</summary>
    public void SetShotFromWorld(Vector3 worldPosition, Quaternion worldRotation)
    {
        Transform space = ShotSpace;
        if (space == null)
        {
            return;
        }

        shotLocalPosition = space.InverseTransformPoint(worldPosition);
        shotLocalEuler = (Quaternion.Inverse(space.rotation) * worldRotation).eulerAngles;
        hasShot = true;
    }

    /// <summary>Where the tool is at a point in the beat.</summary>
    /// <param name="t">Below <c>0</c> the tool waits; <c>0</c>..<c>1</c> walks the swing.</param>
    /// <param name="clock">Seconds driving the wait bob, passed in rather than read from <c>Time.time</c> so edit mode can supply its own.</param>
    /// <returns><c>false</c> when the cut has no plane, leaving the outputs meaningless.</returns>
    public bool TryGetToolPose(float t, float clock, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (Plane == null)
        {
            return false;
        }

        Vector3 approach = ApproachAxis;
        Vector3 sweep = SweepAxis;
        Vector3 center = CutCenter;

        // forward along the sweep, up along the approach, so a prefab modelled edge-down chops the
        // way it looks like it should
        rotation = Quaternion.LookRotation(sweep, approach) * Quaternion.Euler(ToolEuler);

        // a straight lerp between these two reads as a diagonal chop and puts the blade on the cut
        // at ImpactT
        float hover = HoverHeight;
        Vector3 from = center + sweep * SweepDist + approach * hover;
        Vector3 to = center - sweep * SweepDist - approach * (hover * 0.5f);

        if (t < 0f)
        {
            // waits at the swing's own start; anywhere else and the blade jumps to here on the click
            float bob = Mathf.Sin(clock * BobHz * Mathf.PI * 2f) * BobAmp;
            position = from + approach * bob;
            return true;
        }

        AnimationCurve ease = SlashEase;
        float eased = ease != null && ease.length > 0
            ? ease.Evaluate(Mathf.Clamp01(t))
            : Mathf.Clamp01(t);

        position = Vector3.Lerp(from, to, eased);
        return true;
    }

    /// <summary>How far the severed piece appears pushed away at a point in the swing, <c>Vector3.zero</c> before impact.</summary>
    /// <remarks>
    /// Invariant: a display offset only — no physics runs, and nothing is sliced.
    /// <para>Pushed down the cut's <b>-normal</b>, so the piece leaves along the face it was cut from
    /// rather than along the blade's approach — the two differ whenever <see cref="ApproachTilt"/> is
    /// off 90. A small world-up lift is mixed in so it arcs clear of the stump instead of sliding
    /// straight back into it.</para>
    /// </remarks>
    public Vector3 SeveredOffsetAt(float t)
    {
        if (t <= ImpactT)
        {
            return Vector3.zero;
        }

        CutPlane plane = Plane;
        Vector3 normal = plane != null ? plane.Normal : Vector3.up;

        // -normal is the free side of the cut; the up bias is the "little y" that lifts the piece off
        // the stump. Normalized so the bias changes the direction, not the distance Kick sets.
        Vector3 dir = (-normal + Vector3.up * KickUpBias).normalized;

        return dir * (Kick * 0.05f * (t - ImpactT));
    }

    // ---- the beat ----

    /// <summary>Runs the finisher.</summary>
    /// <param name="onImpact">Fires on the frame the blade reaches the cut, which is where the splice belongs.</param>
    /// <param name="onDone">Fires once the swing and its hold are over.</param>
    public void Begin(Action onImpact, Action onDone)
    {
        if (running != null)
        {
            StopCoroutine(running);
            running = null;
        }

        running = StartCoroutine(Run(onImpact, onDone));
    }

    private IEnumerator Run(Action onImpact, Action onDone)
    {
        Camera cam = Manager != null ? Manager.SceneCamera : null;

        Transform tool = EnsureTool(false);

        // ---- 1. ease in ----
        if (cam != null && TryGetCameraPose(out Vector3 shotPos, out Quaternion shotRot, out float shotFOV))
        {
            Vector3 fromPos = cam.transform.position;
            Quaternion fromRot = cam.transform.rotation;
            float fromFOV = cam.fieldOfView;

            float duration = EaseIn;
            AnimationCurve curve = EaseInCurve;

            float elapsed = 0f;
            while (duration > 0f && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / duration);
                float e = curve != null && curve.length > 0 ? curve.Evaluate(raw) : raw;

                cam.transform.SetPositionAndRotation(
                    Vector3.Lerp(fromPos, shotPos, e),
                    Quaternion.Slerp(fromRot, shotRot, e));
                cam.fieldOfView = Mathf.Lerp(fromFOV, shotFOV, e);

                PoseTool(tool, -1f, Time.time);
                yield return null;
            }

            cam.transform.SetPositionAndRotation(shotPos, shotRot);
            cam.fieldOfView = shotFOV;
        }

        // ---- 2. wait, until the player swings ----
        float timeout = AutoSlashAfter;
        float waited = 0f;
        while (!SlashRequested())
        {
            PoseTool(tool, -1f, Time.time);

            waited += Time.deltaTime;
            if (timeout > 0f && waited >= timeout)
            {
                break;
            }

            yield return null;
        }

        // ---- 3. slash, firing the splice on the frame the blade reaches the cut ----

        // the tear leads the splice by half a swing: the splice stalls the main thread on the mesh
        // slice, so a tear fired next to it lands after the piece has already visibly moved. Started
        // here it is under the blade by the time the cut opens.
        if (Manager != null)
        {
            Manager.PlayTearSound();
        }

        float slash = SlashTime;
        bool impacted = false;
        float t = 0f;
        while (t < 1f)
        {
            t = slash > 0f ? Mathf.Min(1f, t + Time.deltaTime / slash) : 1f;
            PoseTool(tool, t, Time.time);

            if (!impacted && t >= ImpactT)
            {
                impacted = true;
                onImpact?.Invoke();

                // the piece does not exist until the callback returns
            }

            yield return null;
        }

        // without this the cut would never splice and the manager would sit in Finishing forever
        if (!impacted)
        {
            onImpact?.Invoke();
            
        }

        // ---- 4. hold on the aftermath, then hand back ----
        float hold = HoldAfter;
        if (hold > 0f)
        {
            yield return new WaitForSeconds(hold);
        }
        // all of it. The cut releases it when the camera lands.
        running = null;
        CuttingManager.currentGame = null;
        onDone?.Invoke();
    }

    /// <summary>Whether the player has asked for the swing this frame.</summary>
    /// <remarks>Invariant: a button already held when the wait begins does not fire the swing — only a fresh press does.</remarks>
    private static bool SlashRequested()
    {
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    }

    /// <summary>Pushes the severed piece away along the approach axis, doing nothing when the slice produced none.</summary>
    

    

    /// <summary>Writes one tool pose, doing nothing when no tool is up.</summary>
    private void PoseTool(Transform tool, float t, float clock)
    {
        if (tool == null)
        {
            return;
        }

        if (TryGetToolPose(t, clock, out Vector3 position, out Quaternion rotation))
        {
            tool.SetPositionAndRotation(position, rotation);
        }
    }

    /// <summary>Readies the tool and hands back the transform the swing drives, or <c>null</c> when none is set.</summary>
    /// <param name="temporary">For a spawned prefab copy, hides it from the hierarchy and keeps it out of the saved scene. Ignored for a scene object, which is driven in place.</param>
    /// <remarks>
    /// A scene object is swung <b>where it already sits</b> -- no copy is made, the assigned transform is
    /// returned directly. Only a prefab ASSET is instantiated, since an asset is not in the scene to
    /// move. This is what lets the saw already placed in the level be dropped into the slot and swing,
    /// rather than only a prefab working.
    /// </remarks>
    public Transform EnsureTool(bool temporary)
    {
        GameObject source = ToolPrefab;
        if (source == null)
        {
            ReleaseTool();
            return null;
        }

        // scene object: drive it in place. IsValid() is the discriminator -- a prefab asset has no scene.
        if (source.scene.IsValid())
        {
            if (drivenTool != source)
            {
                ReleaseTool();
                drivenTool = source;

                // its resting pose, so release puts the saw back rather than leaving it mid-swing
                drivenRestPos = source.transform.position;
                drivenRestRot = source.transform.rotation;
            }

            if (!source.activeSelf) source.SetActive(true);
            return source.transform;
        }

        // ---- prefab asset: spawn a throwaway copy the finisher owns ----

        // a changed prefab has to respawn, or the old tool stays on screen
        if (toolInstance != null && toolInstanceSource != source)
        {
            ReleaseTool();
        }

        if (toolInstance != null)
        {
            return toolInstance.transform;
        }

        toolInstance = Instantiate(source);
        toolInstance.name = $"~{source.name} (finisher)";
        toolInstanceSource = source;

        // a prefab kept switched off until the minigame is often saved with an inactive root, which
        // Instantiate carries over. The finisher shows this copy deliberately, so force it on.
        if (!toolInstance.activeSelf)
        {
            toolInstance.SetActive(true);
        }

        if (temporary)
        {
            // backstop for the paths that never reach a Stop, such as a recompile mid-preview
            toolInstance.hideFlags = HideFlags.HideAndDontSave;
        }

        return toolInstance.transform;
    }

    /// <summary>Puts the tool away: a driven scene object back to its rest pose, a spawned prefab copy destroyed. No-op when none is up.</summary>
    public void ReleaseTool()
    {
        // driven scene object: it belongs to the scene, so restore its pose and let it be
        if (drivenTool != null)
        {
            drivenTool.transform.SetPositionAndRotation(drivenRestPos, drivenRestRot);
            drivenTool = null;
        }

        // spawned copy: the finisher's own, so destroy it
        if (toolInstance != null)
        {
            if (Application.isPlaying)
            {
                Destroy(toolInstance);
            }
            else
            {
                DestroyImmediate(toolInstance);
            }
            toolInstance = null;
        }

        toolInstanceSource = null;
    }

    private void OnDisable()
    {
        // the coroutine stops with the component, so a tool left up would hang in the air
        ReleaseTool();
        running = null;
    }

#if UNITY_EDITOR
    /// <summary>Draws the swing, the wait position and the shot, so the angles read without starting a preview.</summary>
    private void OnDrawGizmosSelected()
    {
        if (Plane == null)
        {
            return;
        }

        Vector3 center = CutCenter;
        Vector3 approach = ApproachAxis;
        Vector3 sweep = SweepAxis;

        float hover = HoverHeight;
        Vector3 from = center + sweep * SweepDist + approach * hover;
        Vector3 to = center - sweep * SweepDist - approach * (hover * 0.5f);

        // the swing, start to end
        Gizmos.color = Color.red;
        Gizmos.DrawLine(from, to);

        // where it waits -- the swing's own start -- and how far it bobs there
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(from, BobAmp);

        // the sweep axis on its own, so the free angle is readable
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center - sweep * SweepDist, center + sweep * SweepDist);

        // where the camera watches from, and what it is pointed at
        if (TryGetCameraPose(out Vector3 shot, out Quaternion aim, out _))
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(shot, 0.03f);
            Gizmos.DrawLine(shot, shot + aim * Vector3.forward * 0.25f);
        }
    }
#endif
}
