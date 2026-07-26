using System.Collections.Generic;
using UnityEngine;

namespace EzySlice {

    /// <summary>Extracts the ordered contour where a <c>Plane</c> cuts a <c>Mesh</c>, without producing any sliced geometry.</summary>
    /// <remarks>
    /// Invariant: recovers concave outlines and multiple disjoint loops, not only convex cross sections.
    /// Invariant: all returned points are in mesh-local space; convert with <c>transform.TransformPoint</c>.
    /// </remarks>
    public static class CutContour {

        /// <summary>One contour extracted from a cut.</summary>
        public struct Loop {
            /// <summary>Ordered contour points, in mesh-local space.</summary>
            public List<Vector3> points;

            /// <summary><c>true</c> for a full loop; <c>false</c> for an open chain left by clipping to a <c>PlaneBounds</c>.</summary>
            /// <remarks>Invariant: on an open chain, no edge joins the last point back to the first.</remarks>
            public bool closed;
        }

        /// <summary>Finite rectangular window on the otherwise infinite cutting plane.</summary>
        /// <remarks>Invariant: contour outside the rectangle is dropped; segments crossing the border are clipped exactly to it.</remarks>
        public struct PlaneBounds {
            /// <summary>Maps a mesh-local point into the rectangle's frame, where the rectangle spans <c>[-halfU,halfU]</c> by <c>[-halfV,halfV]</c> in local X/Z and local Y is the plane normal.</summary>
            public Matrix4x4 meshToBounds;

            /// <summary>Half-extent of the window along local X.</summary>
            public float halfU;

            /// <summary>Half-extent of the window along local Z.</summary>
            public float halfV;

            /// <summary>Trims a mesh-local segment to the rectangle.</summary>
            /// <param name="c0">Trimmed start endpoint; equals <paramref name="p0"/> when the segment lies fully outside.</param>
            /// <param name="c1">Trimmed end endpoint; equals <paramref name="p1"/> when the segment lies fully outside.</param>
            /// <returns><c>false</c> when the segment lies fully outside the rectangle.</returns>
            /// <remarks>Invariant: trimmed endpoints stay on the cutting plane.</remarks>
            public bool ClipSegment(Vector3 p0, Vector3 p1, out Vector3 c0, out Vector3 c1) {
                return ClipSegment(p0, p1, out c0, out c1, out _, out _);
            }

            /// <summary>Trims a mesh-local segment to the rectangle, reporting which endpoints were actually cut back.</summary>
            /// <param name="trimmed0">Whether <paramref name="c0"/> was moved off <paramref name="p0"/> by the border.</param>
            /// <param name="trimmed1">Whether <paramref name="c1"/> was moved off <paramref name="p1"/> by the border.</param>
            public bool ClipSegment(Vector3 p0, Vector3 p1, out Vector3 c0, out Vector3 c1, out bool trimmed0, out bool trimmed1) {
                c0 = p0;
                c1 = p1;
                trimmed0 = false;
                trimmed1 = false;

                Vector3 a0 = meshToBounds.MultiplyPoint3x4(p0);
                Vector3 a1 = meshToBounds.MultiplyPoint3x4(p1);

                float x0 = a0.x, y0 = a0.z;
                float dx = a1.x - x0, dy = a1.z - y0;

                float t0 = 0.0f;
                float t1 = 1.0f;

                if (!ClipEdge(-dx, x0 - (-halfU), ref t0, ref t1)) return false; // x >= -halfU
                if (!ClipEdge( dx, halfU - x0,    ref t0, ref t1)) return false; // x <=  halfU
                if (!ClipEdge(-dy, y0 - (-halfV), ref t0, ref t1)) return false; // z >= -halfV
                if (!ClipEdge( dy, halfV - y0,    ref t0, ref t1)) return false; // z <=  halfV

                trimmed0 = t0 > 0.0f;
                trimmed1 = t1 < 1.0f;
                c0 = Vector3.Lerp(p0, p1, t0);
                c1 = Vector3.Lerp(p0, p1, t1);
                return true;
            }

            /// <summary>Narrows the parameter range <c>[t0,t1]</c> to the inside of one rectangle border.</summary>
            /// <returns><c>false</c> when the segment falls entirely outside this border.</returns>
            private static bool ClipEdge(float p, float q, ref float t0, ref float t1) {
                if (Mathf.Abs(p) < 1e-8f) {
                    // segment parallel to this border: inside only when the origin side is inside
                    return q >= 0.0f;
                }

                float r = q / p;

                if (p < 0.0f) {
                    if (r > t1) return false;
                    if (r > t0) t0 = r;
                } else {
                    if (r < t0) return false;
                    if (r < t1) t1 = r;
                }

                return true;
            }
        }

        /// <summary>Builds a window whose world size is the plane transform's scale times <paramref name="boundsSize"/>.</summary>
        /// <param name="boundsSize">Rectangle size in the plane's local units, before the plane's own scale.</param>
        /// <param name="boundsCenter">Rectangle centre offset in the plane's local units (X, Z), so the window need not be centred on the plane's origin. Its own scale applies the same way the size's does.</param>
        public static PlaneBounds? BuildBounds(Transform planeTransform, Vector2 boundsSize, GameObject meshCut, Vector2 boundsCenter = default) {
            // the offset is applied in the plane's local frame, AFTER worldToLocal, so it is in the
            // same units the half-extents are compared in. Baked into the matrix rather than stored
            // beside it, so every clip test stays two subtractions with no extra term.
            Matrix4x4 meshToPlane = planeTransform.worldToLocalMatrix * meshCut.transform.localToWorldMatrix;
            Matrix4x4 recentre = Matrix4x4.Translate(new Vector3(-boundsCenter.x, 0.0f, -boundsCenter.y));

            return new PlaneBounds {
                meshToBounds = recentre * meshToPlane,
                halfU = boundsSize.x * 0.5f,
                halfV = boundsSize.y * 0.5f,
            };
        }

        /// <summary>Extracts every contour where the plane meets the mesh surface.</summary>
        /// <param name="weld">Distance below which two mesh vertices count as the same vertex (collapses UV/normal seam duplicates); use a small fraction of the mesh's smallest feature.</param>
        /// <param name="bounds">Optional finite window; when set, only the contour inside it is kept and clipped contours come back open.</param>
        /// <returns>Empty when the plane misses the mesh, or nothing survives <paramref name="bounds"/>.</returns>
        /// <remarks>Invariant: segments are connected by the mesh feature they cross (edge or vertex identity), never by intersection-point position — so a loop's closure is topological and survives any plane rotation, where position welding can snap a chain apart on float rounding.</remarks>
        public static List<Loop> ExtractLoops(Mesh mesh, Plane pl, float weld = 1e-4f, PlaneBounds? bounds = null) {
            var loops = new List<Loop>();

            if (mesh == null) {
                return loops;
            }

            Vector3[] verts = mesh.vertices;
            float invWeld = 1.0f / Mathf.Max(weld, 1e-8f);

            // canonical id per vertex POSITION, so seam-duplicated vertices share one id and a
            // mesh edge keeps one identity no matter which triangle (or duplicate pair) names it
            var canonical = new Dictionary<Vector3Int, int>();
            int[] canon = new int[verts.Length];
            for (int i = 0; i < verts.Length; i++) {
                var key = new Vector3Int(
                    Mathf.RoundToInt(verts[i].x * invWeld),
                    Mathf.RoundToInt(verts[i].y * invWeld),
                    Mathf.RoundToInt(verts[i].z * invWeld));
                if (!canonical.TryGetValue(key, out int id)) {
                    id = canonical.Count;
                    canonical.Add(key, id);
                }
                canon[i] = id;
            }

            // graph nodes keyed by the mesh feature the contour point sits on (a crossed edge, or
            // a vertex lying ON the plane). Neighbouring triangles share the crossed edge, so they
            // connect exactly; a clipped endpoint gets a fresh unkeyed node and ends its chain.
            var nodeByFeature = new Dictionary<long, int>();
            var points = new List<Vector3>();
            var adjacency = new List<List<int>>();

            int NodeOf(long feature, Vector3 pos) {
                if (feature >= 0 && nodeByFeature.TryGetValue(feature, out int id)) {
                    return id;
                }
                int fresh = points.Count;
                if (feature >= 0) {
                    nodeByFeature.Add(feature, fresh);
                }
                points.Add(pos);
                adjacency.Add(new List<int>(2));
                return fresh;
            }

            // 1. collect the cut segment (2 points) of every straddling triangle and link the
            //    two mesh features it spans.
            int submeshCount = mesh.subMeshCount;

            for (int submesh = 0; submesh < submeshCount; submesh++) {
                int[] indices = mesh.GetTriangles(submesh);
                int indicesCount = indices.Length;

                for (int index = 0; index < indicesCount; index += 3) {
                    int ia = indices[index + 0];
                    int ib = indices[index + 1];
                    int ic = indices[index + 2];
                    Vector3 a = verts[ia];
                    Vector3 b = verts[ib];
                    Vector3 c = verts[ic];

                    // classify each vertex against the plane
                    SideOfPlane sa = pl.SideOf(a);
                    SideOfPlane sb = pl.SideOf(b);
                    SideOfPlane sc = pl.SideOf(c);

                    // whole triangle on one side (or fully ON) -> no cut segment
                    if (sa == sb && sb == sc) {
                        continue;
                    }

                    // degenerate contact (an edge or face lying on the plane) -> no
                    // proper 2-point segment, skip to avoid junk edges.
                    if ((sa == SideOfPlane.ON && sa == sb) ||
                        (sa == SideOfPlane.ON && sa == sc) ||
                        (sb == SideOfPlane.ON && sb == sc)) {
                        continue;
                    }
                    if ((sa == SideOfPlane.ON && sb != SideOfPlane.ON && sb == sc) ||
                        (sb == SideOfPlane.ON && sa != SideOfPlane.ON && sa == sc) ||
                        (sc == SideOfPlane.ON && sa != SideOfPlane.ON && sa == sb)) {
                        continue;
                    }

                    // gather exactly the two crossing points of this triangle, each tagged with
                    // the mesh feature (edge or ON vertex) it lies on
                    if (!TryGetSegment(pl, canon[ia], canon[ib], canon[ic], a, b, c, sa, sb, sc,
                        out Vector3 p0, out Vector3 p1, out long f0, out long f1)) {
                        continue;
                    }

                    // clip to the finite plane window, if one was supplied; a trimmed endpoint no
                    // longer lies on its mesh feature, so it becomes a fresh chain end
                    if (bounds.HasValue) {
                        if (!bounds.Value.ClipSegment(p0, p1, out p0, out p1, out bool trimmed0, out bool trimmed1)) {
                            continue;
                        }
                        if (trimmed0) f0 = -1;
                        if (trimmed1) f1 = -1;
                    }

                    // ignore degenerate segments that start and end on the same feature
                    if (f0 == f1 && f0 >= 0) {
                        continue;
                    }

                    int id0 = NodeOf(f0, p0);
                    int id1 = NodeOf(f1, p1);
                    if (id0 == id1) {
                        continue;
                    }

                    LinkOnce(adjacency, id0, id1);
                }
            }

            if (points.Count < 2) {
                return loops;
            }

            // 2. walk the edge graph into ordered contours. Open chains (degree-1
            //    endpoints, produced by clipping) are walked first so each is captured
            //    whole from one end; closed loops (all degree 2) are picked up after.
            bool[] visited = new bool[points.Count];

            for (int start = 0; start < points.Count; start++) {
                if (visited[start] || adjacency[start].Count != 1) {
                    continue;
                }

                Loop chain = WalkLoop(start, points, adjacency, visited);
                if (chain.points.Count >= 2) {
                    loops.Add(chain);
                }
            }

            for (int start = 0; start < points.Count; start++) {
                if (visited[start] || adjacency[start].Count == 0) {
                    continue;
                }

                Loop loop = WalkLoop(start, points, adjacency, visited);
                if (loop.points.Count >= 3) {
                    loops.Add(loop);
                }
            }

            return loops;
        }

        /// <summary>Extracts the single largest contour, by point count.</summary>
        /// <returns><c>null</c> when the plane does not cut the mesh, or nothing survives <paramref name="bounds"/>.</returns>
        public static List<Vector3> Extract(Mesh mesh, Plane pl, float weld = 1e-4f, PlaneBounds? bounds = null) {
            List<Loop> loops = ExtractLoops(mesh, pl, weld, bounds);

            List<Vector3> largest = null;

            for (int i = 0; i < loops.Count; i++) {
                if (largest == null || loops[i].points.Count > largest.Count) {
                    largest = loops[i].points;
                }
            }

            return largest;
        }

        /// <summary>Extracts contours by cutting the object's mesh with a plane given in world space.</summary>
        /// <param name="worldPos">Point on the cutting plane, in world space.</param>
        /// <param name="worldDir">Plane normal, in world space.</param>
        /// <returns>Empty when the object has no <c>MeshFilter</c> with a shared mesh; a warning is logged.</returns>
        public static List<Loop> ExtractLoops(GameObject obj, Vector3 worldPos, Vector3 worldDir, float weld = 1e-4f, PlaneBounds? bounds = null) {
            if (obj == null || !obj.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) {
                Debug.LogWarning("EzySlice::CutContour -> GameObject must have a MeshFilter with a valid sharedMesh.");
                return new List<Loop>();
            }

            // transform the world-space plane into mesh-local space
            Matrix4x4 mat = obj.transform.worldToLocalMatrix;
            Matrix4x4 inv = mat.transpose.inverse;

            Vector3 refUp = inv.MultiplyVector(worldDir).normalized;
            Vector3 refPt = obj.transform.InverseTransformPoint(worldPos);

            Plane cuttingPlane = new Plane(refPt, refUp);

            return ExtractLoops(filter.sharedMesh, cuttingPlane, weld, bounds);
        }

        /// <summary>Offsets a contour into inner and outer polylines, forming a thin band around it.</summary>
        /// <param name="closed">Whether the contour is a closed loop; an open chain keeps square ends.</param>
        /// <param name="normal">Cutting-plane normal, in the same space as <paramref name="pts"/>.</param>
        /// <param name="width">Full band width; each side is offset by half of it.</param>
        /// <param name="inner">Receives the inward-offset polyline; cleared first.</param>
        /// <param name="outer">Receives the outward-offset polyline; cleared first.</param>
        public static void Ribbon(List<Vector3> pts, bool closed, Vector3 normal, float width,
            List<Vector3> inner, List<Vector3> outer) {

            inner.Clear();
            outer.Clear();

            int n = pts.Count;
            if (n < 2) {
                return;
            }

            float half = width * 0.5f;
            Vector3 nrm = normal.normalized;

            for (int i = 0; i < n; i++) {
                Vector3 e0, e1;

                if (closed) {
                    e0 = pts[i] - pts[(i - 1 + n) % n];
                    e1 = pts[(i + 1) % n] - pts[i];
                } else if (i == 0) {
                    e1 = pts[1] - pts[0];
                    e0 = e1;
                } else if (i == n - 1) {
                    e0 = pts[n - 1] - pts[n - 2];
                    e1 = e0;
                } else {
                    e0 = pts[i] - pts[i - 1];
                    e1 = pts[i + 1] - pts[i];
                }

                Vector3 off = Bisector(e0, e1, nrm, half);
                inner.Add(pts[i] - off);
                outer.Add(pts[i] + off);
            }
        }

        /// <summary>Offset vector at a contour vertex that holds the band to a constant width through the corner.</summary>
        private static Vector3 Bisector(Vector3 e0, Vector3 e1, Vector3 normal, float half) {
            Vector3 n0 = Vector3.Cross(normal, e0.normalized);
            Vector3 n1 = Vector3.Cross(normal, e1.normalized);

            Vector3 m = n0 + n1;
            float mag = m.magnitude;

            // near-180 fold: bisector collapses, fall back to one edge normal
            if (mag < 1e-6f) {
                return n1 * half;
            }

            m /= mag;

            float d = Vector3.Dot(m, n1);
            if (Mathf.Abs(d) < 1e-4f) {
                d = 1.0f;
            }

            float miter = Mathf.Clamp(half / d, -half * 4.0f, half * 4.0f);
            return m * miter;
        }

        /// <summary>Encodes a mesh edge between two canonical vertex ids as a graph-node feature key, disjoint from single-vertex keys.</summary>
        private static long EdgeFeature(int i, int j) {
            long lo = Mathf.Min(i, j) + 1;
            long hi = Mathf.Max(i, j) + 1;
            return (lo << 32) | (uint)hi;
        }

        /// <summary>Finds the two points where the plane crosses the edges of triangle <c>a-b-c</c>, tagging each with the mesh feature it lies on (crossed edge, or ON vertex).</summary>
        /// <param name="ca">Canonical id of vertex <paramref name="a"/> (likewise <paramref name="cb"/>, <paramref name="cc"/>).</param>
        /// <param name="f0">Feature key of <paramref name="p0"/>: canonical vertex id for an ON vertex, <see cref="EdgeFeature"/> for a crossed edge.</param>
        /// <returns><c>false</c> unless exactly two crossing points were found.</returns>
        private static bool TryGetSegment(Plane pl,
            int ca, int cb, int cc,
            Vector3 a, Vector3 b, Vector3 c,
            SideOfPlane sa, SideOfPlane sb, SideOfPlane sc,
            out Vector3 p0, out Vector3 p1, out long f0, out long f1) {

            p0 = Vector3.zero;
            p1 = Vector3.zero;
            f0 = -1;
            f1 = -1;

            int found = 0;

            // a vertex sitting ON the plane is itself a contour point
            if (sa == SideOfPlane.ON) { p0 = a; f0 = ca; found++; }
            if (sb == SideOfPlane.ON) { if (found == 0) { p0 = b; f0 = cb; } else { p1 = b; f1 = cb; } found++; }
            if (sc == SideOfPlane.ON) { if (found == 0) { p0 = c; f0 = cc; } else { p1 = c; f1 = cc; } found++; }

            // edges that straddle the plane (opposite non-ON sides) contribute a point
            if (found < 2 && Crosses(sa, sb) && Intersector.Intersect(pl, a, b, out Vector3 q0)) {
                if (found == 0) { p0 = q0; f0 = EdgeFeature(ca, cb); } else { p1 = q0; f1 = EdgeFeature(ca, cb); }
                found++;
            }
            if (found < 2 && Crosses(sb, sc) && Intersector.Intersect(pl, b, c, out Vector3 q1)) {
                if (found == 0) { p0 = q1; f0 = EdgeFeature(cb, cc); } else { p1 = q1; f1 = EdgeFeature(cb, cc); }
                found++;
            }
            if (found < 2 && Crosses(sc, sa) && Intersector.Intersect(pl, c, a, out Vector3 q2)) {
                if (found == 0) { p0 = q2; f0 = EdgeFeature(cc, ca); } else { p1 = q2; f1 = EdgeFeature(cc, ca); }
                found++;
            }

            return found == 2;
        }

        /// <summary>Whether the two endpoints sit on opposite sides of the plane.</summary>
        private static bool Crosses(SideOfPlane s0, SideOfPlane s1) {
            return (s0 == SideOfPlane.UP && s1 == SideOfPlane.DOWN) ||
                   (s0 == SideOfPlane.DOWN && s1 == SideOfPlane.UP);
        }

        /// <summary>Links two vertices as mutual neighbours, ignoring a link that already exists.</summary>
        private static void LinkOnce(List<List<int>> adjacency, int i, int j) {
            if (!adjacency[i].Contains(j)) {
                adjacency[i].Add(j);
            }
            if (!adjacency[j].Contains(i)) {
                adjacency[j].Add(i);
            }
        }

        /// <summary>Walks neighbour links from a start vertex into one ordered contour.</summary>
        /// <returns>A closed <c>Loop</c> when the walk returns to the start, otherwise an open chain.</returns>
        /// <remarks>Invariant: every visited vertex is marked, so each vertex lands in exactly one contour.</remarks>
        private static Loop WalkLoop(int start,
            List<Vector3> points,
            List<List<int>> adjacency,
            bool[] visited) {

            var loop = new Loop { points = new List<Vector3>(), closed = false };

            int current = start;
            int previous = -1;

            while (current != -1 && !visited[current]) {
                visited[current] = true;
                loop.points.Add(points[current]);

                // pick the next neighbour that isn't where we just came from
                int next = -1;
                List<int> neighbours = adjacency[current];

                for (int i = 0; i < neighbours.Count; i++) {
                    int candidate = neighbours[i];

                    if (candidate == previous) {
                        continue;
                    }

                    // closing the loop back to the start is the desired terminator
                    if (candidate == start) {
                        loop.closed = true;
                        return loop;
                    }

                    if (!visited[candidate]) {
                        next = candidate;
                        break;
                    }
                }

                previous = current;
                current = next;
            }

            return loop;
        }

        /// <summary>Pushes every point outward from the contour centre by <paramref name="scale"/>.</summary>
        /// <param name="scale">Outward distance added to each point along its direction from the centre.</param>
        /// <returns>A new list; the input is left unchanged.</returns>
        public static List<Vector3> ScaleLoop(List<Vector3> points, float scale) {
            Vector3 center = GetCenter(points);
            List<Vector3> scaledPoints = new List<Vector3>(points);
            for (int i = 0; i < points.Count; i++) {
                scaledPoints[i] += (scaledPoints[i] - center).normalized * scale;
            }

            return scaledPoints;
        }

        /// <summary>Averages the points to their centre.</summary>
        /// <returns><c>Vector3.zero</c> when the list is null or empty.</returns>
        public static Vector3 GetCenter(List<Vector3> points) {
            if (points == null || points.Count == 0)
                return Vector3.zero;

            Vector3 sum = Vector3.zero;

            foreach (Vector3 point in points)
                sum += point;

            return sum / points.Count;
        }
    }
}
