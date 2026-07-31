using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Lays the player's cut line: raycasts the mouse onto the cut target and appends surface points to a LineRenderer.</summary>
/// <remarks>Traces itself every frame while <see cref="trace"/> is on, independent of camera speed.</remarks>
public class CutTracer : MonoBehaviour {

    [Tooltip("Object being cut. Only raycast hits on this object add a point.")]
    public GameObject cutTarget;

    public LineRenderer Lrenderer;

    [Tooltip("Lift each stored point off the surface along its normal, in world units.")]
    public float pointHoverLenght = 0.1f;

    public float LineWitdth = 0.005f;

    public List<Vector3> cutPoints = new List<Vector3>();

    public bool trace = true;

    void Start()
    {
        ApplyLineWidth();
    }

#if UNITY_EDITOR
    // guards against stacking one deferred apply per OnValidate call; not serialized, purely edit-time.
    [System.NonSerialized] private bool _widthApplyQueued;
#endif

    /// <remarks>
    /// The width lives on another object -- the LineRenderer -- and OnValidate also fires during a prefab
    /// apply and the reimport it triggers. Writing to another object from there fights that operation (an
    /// override applied to the prefab reverts immediately), and Unity documents OnValidate as not
    /// modifying other objects. So the write is deferred out of validation: the apply settles, then the
    /// delegate runs once and re-checks this component still exists.
    /// </remarks>
    void OnValidate()
    {
#if UNITY_EDITOR
        if (_widthApplyQueued) return;
        _widthApplyQueued = true;
        UnityEditor.EditorApplication.delayCall += RunDeferredWidthApply;
#endif
    }

#if UNITY_EDITOR
    private void RunDeferredWidthApply()
    {
        _widthApplyQueued = false;

        if (this == null) return; // destroyed between the validate and this callback
        ApplyLineWidth();
    }
#endif

    /// <summary>Writes <see cref="LineWitdth"/> onto the line, undoably in edit mode.</summary>
    /// <remarks>No-op without a renderer: the slot is assigned by hand, and an unwired tracer used to
    /// throw a null reference on every validate and on every load.</remarks>
    private void ApplyLineWidth()
    {
        if (Lrenderer == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(Lrenderer, "Set cut trace width");
        }
#endif

        Lrenderer.widthCurve = AnimationCurve.Constant(0, 1, LineWitdth);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(Lrenderer);
        }
#endif
    }

    void Update()
    {
        Trace();
    }

    /// <summary>Raycasts the mouse into the scene; if it hits <c>cutTarget</c>, stores the hover-offset surface point.</summary>
    public void Trace()
    {
        if(!trace) return;
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Debug.Log("Hit: " + hit.collider.name);
            Debug.Log("Position: " + hit.point);
            if (cutTarget == hit.collider.gameObject)
            {
                AddPoint(hit.point + hit.normal * pointHoverLenght);
            }
        }
    }

    void AddPoint(Vector3 point)
    {
        cutPoints.Add(point);

        Lrenderer.positionCount = cutPoints.Count;
        Lrenderer.SetPositions(cutPoints.ToArray());
    }

    [ContextMenu("reset points")]
    void ClearPoints()
    {
        Lrenderer.positionCount = 0;
        cutPoints.Clear();
    }
}
