using System.Collections.Generic;
using UnityEngine;
using static EzySlice.CutContourAuthoring;

public static class GizmoUtils
{
    /// <summary>
    /// Draws a line between pointsA[i] and pointsB[i] for each matching index.
    /// Call only from OnDrawGizmos or OnDrawGizmosSelected.
    /// </summary>
    public static void DrawPointPairs(
        IList<Vector3> pointsA,
        IList<Vector3> pointsB,
        Color color)
    {

        if (pointsA == null || pointsB == null)
            return;

        Color previousColor = Gizmos.color;
        Gizmos.color = color;

        int count = Mathf.Min(pointsA.Count, pointsB.Count);

        for (int i = 0; i < count; i++)
        {
            Gizmos.DrawLine(pointsA[i], pointsB[i]);
        }

        Gizmos.color = previousColor;
    }
    /// <summary>Draws one loop, connecting the last point to the first only when it is closed.</summary>
    /// <param name="withDots">Whether to mark each vertex with a sphere.</param>
    public static void DrawLoop(Transform tf, SavedLoop loop, Color color, bool withDots) {
        int count = loop.points.Count;
        if (count < 2) return;

        // open chains draw count-1 edges (no last->first); closed draw all
        int edges = loop.closed ? count : count - 1;

        for (int i = 0; i < edges; i++) {
            Vector3 a = tf.TransformPoint(loop.points[i]);
            Vector3 b = tf.TransformPoint(loop.points[(i + 1) % count]);
            Gizmos.DrawLine(a, b);
            if (withDots) {
                Gizmos.DrawSphere(a, 0.01f);
            }
        }

        if (withDots && !loop.closed) {
            Gizmos.DrawSphere(tf.TransformPoint(loop.points[count - 1]), 0.01f);
        }
    }
    public static void DrawBoundsGizmo(Transform planeTransform , Vector2 boundsSize) {
        

        Color prev = Gizmos.color;
        Matrix4x4 prevMat = Gizmos.matrix;

        Gizmos.matrix = Matrix4x4.TRS(planeTransform.position, planeTransform.rotation, planeTransform.lossyScale);
        Gizmos.color = Color.cyan;
        // plane spans right(X)/forward(Z); normal is up(Y)
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(boundsSize.x, 0.0f, boundsSize.y));

        Gizmos.color = prev;
        Gizmos.matrix = prevMat;
    }
    
}