using UnityEngine;
using static LoopFollowingObject;


[CreateAssetMenu(fileName = "FollowLoopPresets", menuName = "FollowLoopPresets", order = 0)]
public class FollowLoopPresets : ScriptableObject
{
    [Tooltip("Which input drives left/right travel: mouse motion, left/right mouse buttons, or the left/right arrow keys.")]
    public MoveInput moveInput = MoveInput.MouseDelta;

    public float Xspeed;

    [Tooltip("How fast the scalpel eases onto its surface target. Higher = snappier, lower = smoother glide. 0 = snap instantly.")]
    public float followSmooth = 20f;

    [Tooltip("How fast the surface normal (used for the hover lift) smooths out. Higher = hugs each mesh facet (more jagged), lower = ignores small facets (smoother).")]
    public float normalSmooth = 15f;

    [Tooltip("Two ways to walk along the limb: translate straight along the plane normal, or rotate the radial about the in-plane tangent (offset in degrees).")]
    public SlideAxis slideAxis = SlideAxis.PlaneNormal;




}