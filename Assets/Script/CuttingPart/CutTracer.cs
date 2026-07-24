using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Lays the player's cut line: raycasts the mouse onto the cut target and appends surface points to a LineRenderer.</summary>
/// <remarks>Driven by <see cref="CuttingSkin"/>, which calls <see cref="Trace"/> only while the camera is actually moving, so coasting between wheel ridges still lays a continuous cut.</remarks>
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
        Lrenderer.widthCurve = AnimationCurve.Constant(0, 1, LineWitdth);
    }

    void OnValidate()
    {
        Lrenderer.widthCurve = AnimationCurve.Constant(0, 1, LineWitdth);
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
