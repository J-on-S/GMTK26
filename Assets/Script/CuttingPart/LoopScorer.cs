using System.Collections.Generic;
using UnityEngine;

public static class LoopScorer
{
  
    public static void ScoreLoop(List<Vector3> attemptPoints , List<Vector3> actualPoints, int numSamplePoints)
    {
        numSamplePoints = Mathf.Clamp(numSamplePoints,0, attemptPoints.Count);
    // simply compare each point position against each other. maybe get average distance 

    }


    public static float SampledLength(IReadOnlyList<Vector3> points) {
            if (points == null || points.Count < 2) return 0f;
            float total = 0f;
            for (int i = 1; i < points.Count; i++) {
                total += Vector3.Distance(points[i - 1], points[i]);
            }
            return total;
    }

    /// <summary>Projects a world-space point onto a polyline and returns the closest point on it.</summary>
    /// <param name="points">Polyline vertices to project against, ordered head-to-tail.</param>
    /// <param name="query">World-space point being projected.</param>
    /// <param name="tAlong">Output: how far along the polyline the closest point lies, normalized over total arc-length (<c>0</c> = at <c>points[0]</c>, <c>1</c> = at the last vertex).</param>
    /// <param name="sqrDist">Output: squared world-space distance from <paramref name="query"/> to the projected point.</param>
    /// <remarks>
    /// Invariant: on a zero-length or single-vertex polyline, returns the first vertex with <c>tAlong=0</c>.
    /// </remarks>
    public static Vector3 ClosestPointOnPolyline(IReadOnlyList<Vector3> points, Vector3 query, out float tAlong, out float sqrDist) {
        tAlong = 0f;
        sqrDist = float.MaxValue;
        if (points == null || points.Count == 0) return Vector3.zero;
        if (points.Count == 1) {
            sqrDist = (query - points[0]).sqrMagnitude;
            return points[0];
        }

        float totalLength = SampledLength(points);
        if (totalLength <= Mathf.Epsilon) {
            sqrDist = (query - points[0]).sqrMagnitude;
            return points[0];
        }

        Vector3 best = points[0];
        float bestSqr = float.MaxValue;
        float bestDistAlong = 0f;
        float acc = 0f;

        for (int i = 1; i < points.Count; i++) {
            Vector3 a = points[i - 1];
            Vector3 b = points[i];
            Vector3 ab = b - a;
            float abLen = ab.magnitude;
            if (abLen <= Mathf.Epsilon) continue;

            float segT = Vector3.Dot(query - a, ab) / (abLen * abLen);
            segT = Mathf.Clamp01(segT);
            Vector3 candidate = a + segT * ab;
            float d = (query - candidate).sqrMagnitude;

            if (d < bestSqr) {
                bestSqr = d;
                best = candidate;
                bestDistAlong = acc + segT * abLen;
            }
            acc += abLen;
        }

        sqrDist = bestSqr;
        tAlong = bestDistAlong / totalLength;
        return best;
    }

    public static Vector3 EvaluateFromSamples(IReadOnlyList<Vector3> points, float t) {
            if (points == null || points.Count == 0) return Vector3.zero;
            if (points.Count == 1) return points[0];

            if (t <= 0f) return points[0];
            if (t >= 1f) return points[points.Count - 1];

            float totalLength = 0f;
            for (int i = 1; i < points.Count; i++) {
                totalLength += Vector3.Distance(points[i - 1], points[i]);
            }
            if (totalLength <= Mathf.Epsilon) return points[0];

            float target = t * totalLength;
            float acc = 0f;
            for (int i = 1; i < points.Count; i++) {
                float seg = Vector3.Distance(points[i - 1], points[i]);
                if (acc + seg >= target) {
                    float segT = seg > Mathf.Epsilon ? (target - acc) / seg : 0f;
                    return Vector3.Lerp(points[i - 1], points[i], segT);
                }
                acc += seg;
            }

            return points[points.Count - 1];
        }

}


// there is a point on the body, move left/right with mousePosition and it will move on the body.
// forward movement is controlled by the camera mouseWheel, only left and right movement is controlled by mouse position x 