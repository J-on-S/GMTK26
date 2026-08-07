using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EzySlice {
    /**
     * Define Extension methods for easy access to slicer functionality
     */
    public static class SlicerExtensions {

        /**
         * SlicedHull Return functions and appropriate overrides!
         */
        public static SlicedHull Slice(this GameObject obj, Plane pl, Material crossSectionMaterial = null) {
            return Slice(obj, pl, new TextureRegion(0.0f, 0.0f, 1.0f, 1.0f), crossSectionMaterial);
        }

        public static SlicedHull Slice(this GameObject obj, Vector3 position, Vector3 direction, Material crossSectionMaterial = null) {
            return Slice(obj, position, direction, new TextureRegion(0.0f, 0.0f, 1.0f, 1.0f), crossSectionMaterial);
        }

        public static SlicedHull Slice(this GameObject obj, Vector3 position, Vector3 direction, TextureRegion textureRegion, Material crossSectionMaterial = null) {
            Plane cuttingPlane = new Plane();

            Matrix4x4 mat = obj.transform.worldToLocalMatrix;
            Matrix4x4 transpose = mat.transpose;
            Matrix4x4 inv = transpose.inverse;

            Vector3 refUp = inv.MultiplyVector(direction).normalized;
            Vector3 refPt = obj.transform.InverseTransformPoint(position);

            cuttingPlane.Compute(refPt, refUp);

            return Slice(obj, cuttingPlane, textureRegion, crossSectionMaterial);
        }

        public static SlicedHull Slice(this GameObject obj, Plane pl, TextureRegion textureRegion, Material crossSectionMaterial = null) {
            return Slicer.Slice(obj, pl, textureRegion, crossSectionMaterial);
        }

        /**
         * These functions (and overrides) will return the final indtaniated GameObjects types
         */
        public static GameObject[] SliceInstantiate(this GameObject obj, Plane pl) {
            return SliceInstantiate(obj, pl, new TextureRegion(0.0f, 0.0f, 1.0f, 1.0f));
        }

        public static GameObject[] SliceInstantiate(this GameObject obj, Vector3 position, Vector3 direction) {
            return SliceInstantiate(obj, position, direction, null);
        }

        public static GameObject[] SliceInstantiate(this GameObject obj, Vector3 position, Vector3 direction, Material crossSectionMat) {
            return SliceInstantiate(obj, position, direction, new TextureRegion(0.0f, 0.0f, 1.0f, 1.0f), crossSectionMat);
        }

        public static GameObject[] SliceInstantiate(this GameObject obj, Vector3 position, Vector3 direction, TextureRegion cuttingRegion, Material crossSectionMaterial = null) {
            EzySlice.Plane cuttingPlane = new EzySlice.Plane();

            Matrix4x4 mat = obj.transform.worldToLocalMatrix;
            Matrix4x4 transpose = mat.transpose;
            Matrix4x4 inv = transpose.inverse;

            Vector3 refUp = inv.MultiplyVector(direction).normalized;
            Vector3 refPt = obj.transform.InverseTransformPoint(position);

            cuttingPlane.Compute(refPt, refUp);

            return SliceInstantiate(obj, cuttingPlane, cuttingRegion, crossSectionMaterial);
        }

        public static GameObject[] SliceInstantiate(this GameObject obj, Plane pl, TextureRegion cuttingRegion, Material crossSectionMaterial = null) {
            SlicedHull slice = Slicer.Slice(obj, pl, cuttingRegion, crossSectionMaterial);

            if (slice == null) {
                return null;
            }

            GameObject upperHull = slice.CreateUpperHull(obj, crossSectionMaterial);
            GameObject lowerHull = slice.CreateLowerHull(obj, crossSectionMaterial);

            if (upperHull != null && lowerHull != null) {
                return new GameObject[] { upperHull, lowerHull };
            }

            // otherwise return only the upper hull
            if (upperHull != null) {
                return new GameObject[] { upperHull };
            }

            // otherwise return only the lower hull
            if (lowerHull != null) {
                return new GameObject[] { lowerHull };
            }

            // nothing to return, so return nothing!
            return null;
        }

        /**
         * Weld the two hulls of a slice back into a single mesh, undoing the cut wherever it is
         * not wanted. Coincident seam vertices are merged so the two outer surfaces reconnect.
         */

        /// <summary>Merges a slice's two hulls into one mesh, re-welding the cut everywhere outside <paramref name="window"/> and dropping the cross-section caps there, so only the region inside the window stays cut.</summary>
        /// <param name="sliced">Result of a <c>Slice</c>. Both hulls must be in <paramref name="original"/>'s local space (they are, straight from the slicer).</param>
        /// <param name="original">The object that was sliced; supplies the original submesh count so the appended cross-section submesh can be told from the skin.</param>
        /// <param name="window">Finite rectangle (mesh-local, from <c>CutContour.BuildBounds</c>) the cut is kept inside. <c>null</c> welds everything and drops every cap, fully rejoining the mesh with no cut left.</param>
        /// <param name="weld">Distance below which two vertices merge; match the value used when extracting the contour.</param>
        /// <returns>One welded mesh (positions + UVs, normals recalculated), or <c>null</c> when the slice produced no geometry.</returns>
        /// <remarks>
        /// Requires slicing with a distinct cross-section material (or a null one), so each hull's cap sits in its own trailing submesh; a cap batched into an existing submesh can't be separated.
        /// Output submeshes: the original skin submeshes in order, then one cap submesh (only if any cap survived). Assign materials in the same order, cross-section material last.
        /// Welding by position collapses split normals and UV seams along the weld; fine for organic meshes, flattening for hard-surface. The result is one connected mesh — no rigidbody separation.
        /// </remarks>
        public static Mesh WeldWithinBounds(this SlicedHull sliced, GameObject original, CutContour.PlaneBounds? window, float weld = 1e-4f) {
            if (sliced == null || original == null) {
                return null;
            }

            int origSubmeshes = 1;
            if (original.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null) {
                origSubmeshes = filter.sharedMesh.subMeshCount;
            }

            var outVerts = new List<Vector3>();
            var outUVs = new List<Vector2>();
            // one triangle bucket per skin submesh, plus a shared cap bucket at the end
            var skin = new List<int>[origSubmeshes];
            for (int i = 0; i < origSubmeshes; i++) {
                skin[i] = new List<int>();
            }
            var cap = new List<int>();

            // merged-vertex table for the welded (outside-window) region only
            var lookup = new Dictionary<Vector3Int, int>();
            float invWeld = 1.0f / Mathf.Max(weld, 1e-8f);

            AppendHull(sliced.upperHull, origSubmeshes, window, invWeld, lookup, outVerts, outUVs, skin, cap);
            AppendHull(sliced.lowerHull, origSubmeshes, window, invWeld, lookup, outVerts, outUVs, skin, cap);

            if (outVerts.Count == 0) {
                return null;
            }

            bool hasUV = outUVs.Count == outVerts.Count;
            bool keepCap = cap.Count > 0;
            int submeshCount = origSubmeshes + (keepCap ? 1 : 0);

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(outVerts);
            if (hasUV) {
                mesh.SetUVs(0, outUVs);
            }
            mesh.subMeshCount = submeshCount;
            for (int i = 0; i < origSubmeshes; i++) {
                mesh.SetTriangles(skin[i], i);
            }
            if (keepCap) {
                mesh.SetTriangles(cap, origSubmeshes);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Welds a slice's two hulls into one solid mesh with no cut left, dropping every cross-section cap. Shorthand for <see cref="WeldWithinBounds"/> with no window.</summary>
        public static Mesh WeldWhole(this SlicedHull sliced, GameObject original, float weld = 1e-4f) {
            return WeldWithinBounds(sliced, original, null, weld);
        }

        /// <summary>Copies one hull's triangles into the shared buffers, welding vertices outside the window and dropping cap triangles outside it.</summary>
        private static void AppendHull(Mesh hull, int origSubmeshes, CutContour.PlaneBounds? window,
            float invWeld, Dictionary<Vector3Int, int> lookup,
            List<Vector3> outVerts, List<Vector2> outUVs, List<int>[] skin, List<int> cap) {

            if (hull == null) {
                return;
            }

            Vector3[] verts = hull.vertices;
            Vector2[] uv = hull.uv;
            bool hasUV = uv.Length == verts.Length;

            for (int s = 0; s < hull.subMeshCount; s++) {
                bool isCap = s >= origSubmeshes;
                List<int> bucket = isCap ? cap : skin[s];

                int[] tri = hull.GetTriangles(s);
                for (int t = 0; t < tri.Length; t += 3) {
                    Vector3 p0 = verts[tri[t]];
                    Vector3 p1 = verts[tri[t + 1]];
                    Vector3 p2 = verts[tri[t + 2]];

                    // an unwanted cap face sits outside the window: drop it so no cut shows there
                    if (isCap && !Inside(window, (p0 + p1 + p2) / 3.0f)) {
                        continue;
                    }

                    int i0 = VertexId(p0, hasUV ? uv[tri[t]] : default, window, invWeld, lookup, outVerts, outUVs);
                    int i1 = VertexId(p1, hasUV ? uv[tri[t + 1]] : default, window, invWeld, lookup, outVerts, outUVs);
                    int i2 = VertexId(p2, hasUV ? uv[tri[t + 2]] : default, window, invWeld, lookup, outVerts, outUVs);

                    // skip triangles that welding collapsed to a line or point
                    if (i0 == i1 || i1 == i2 || i0 == i2) {
                        continue;
                    }

                    bucket.Add(i0);
                    bucket.Add(i1);
                    bucket.Add(i2);
                }
            }
        }

        /// <summary>Returns a vertex id for a point: a fresh unique id inside the window (keeps the cut open), a weld-merged id outside it (rejoins the surfaces).</summary>
        private static int VertexId(Vector3 p, Vector2 uv, CutContour.PlaneBounds? window,
            float invWeld, Dictionary<Vector3Int, int> lookup, List<Vector3> outVerts, List<Vector2> outUVs) {

            if (Inside(window, p)) {
                int fresh = outVerts.Count;
                outVerts.Add(p);
                outUVs.Add(uv);
                return fresh;
            }

            var key = new Vector3Int(
                Mathf.RoundToInt(p.x * invWeld),
                Mathf.RoundToInt(p.y * invWeld),
                Mathf.RoundToInt(p.z * invWeld));

            if (lookup.TryGetValue(key, out int existing)) {
                return existing;
            }

            int id = outVerts.Count;
            lookup.Add(key, id);
            outVerts.Add(p);
            outUVs.Add(uv);
            return id;
        }

        /// <summary>Whether a mesh-local point falls inside the finite cut window. A null window is treated as empty, so every point is "outside" and everything welds.</summary>
        private static bool Inside(CutContour.PlaneBounds? window, Vector3 meshLocalPoint) {
            if (!window.HasValue) {
                return false;
            }
            CutContour.PlaneBounds b = window.Value;
            Vector3 a = b.meshToBounds.MultiplyPoint3x4(meshLocalPoint);
            return Mathf.Abs(a.x) <= b.halfU && Mathf.Abs(a.z) <= b.halfV;
        }

        /// <summary>Splits a slice for a windowed cut: every distinct lower-hull chunk owning a closed in-window cut loop comes out as its own removed piece, while every other slice fragment welds back so the rest rejoins into one body.</summary>
        /// <param name="sliced">Result of a <c>Slice</c>, in <paramref name="original"/>'s local space.</param>
        /// <param name="original">The object that was sliced; supplies the original mesh for contour extraction and the submesh count that tells each hull's appended cap submesh from the skin.</param>
        /// <param name="plane">The cutting plane in <paramref name="original"/>'s mesh-local space — the same plane the slice was made with.</param>
        /// <param name="window">Finite rectangle (mesh-local, from <c>CutContour.BuildBounds</c>) marking the intended cuts. Use a huge window to remove every cut chunk.</param>
        /// <param name="weld">Distance below which coincident vertices merge; match the contour weld.</param>
        /// <param name="body">The body: the whole upper hull plus every lower-hull chunk without a closed in-window loop, welded together, capped along each removed piece's loop. <c>null</c> when empty.</param>
        /// <param name="pieces">Cleared, then filled with one mesh per distinct removed chunk (one per connected lower-hull component owning at least one closed in-window loop). Empty when nothing was cut inside the window.</param>
        /// <remarks>
        /// Caps are rebuilt per contour loop with <see cref="Triangulator.EarClip"/> — the slicer's own cap is one convex hull spanning every cross-section at once, which both bridges disjoint loops with phantom geometry and (being a fan from a single vertex) breaks per-chunk cap attribution. The slicer caps are therefore discarded entirely.
        /// Pieces are found by connectivity, not position: an infinite cutting plane also slices other limbs at the same height, and those stray chunks weld back to the body because they own no closed in-window loop. Each closed loop caps its chunk on both sides of the cut.
        /// Open loops — contours the window clips — are discarded: no piece, no cap, their cut welds shut again.
        /// Every mesh: original skin submeshes in order, then one cap submesh if a cap survived — assign materials the same way, cross-section last.
        /// </remarks>
        public static void SliceWindowedSplit(this SlicedHull sliced, GameObject original, Plane plane,
            CutContour.PlaneBounds? window, float weld, out Mesh body, List<Mesh> pieces) {

            body = null;
            pieces.Clear();
            if (sliced == null || original == null) {
                return;
            }

            Mesh originalMesh = null;
            int origSubmeshes = 1;
            if (original.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null) {
                originalMesh = filter.sharedMesh;
                origSubmeshes = originalMesh.subMeshCount;
            }

            var bodyB = new HullBuilder(origSubmeshes, weld);
            var pieceByRoot = new Dictionary<int, HullBuilder>();

            // upper hull: all skin joins the body; its cut faces are rebuilt per loop below
            SplitUpper(sliced.upperHull, origSubmeshes, bodyB);
            // lower hull: each chunk owning a closed in-window loop becomes its own piece; strays rejoin the body
            SplitLowerByLoops(sliced.lowerHull, originalMesh, origSubmeshes, plane, window, weld, bodyB, pieceByRoot);

            body = bodyB.Build();
            foreach (HullBuilder pieceB in pieceByRoot.Values) {
                Mesh m = pieceB.Build();
                if (m != null) {
                    pieces.Add(m);
                }
            }
        }

        /// <summary>Routes an upper hull's skin into the body. The slicer's cap triangles are dropped — the body's cut faces are rebuilt per closed loop instead.</summary>
        private static void SplitUpper(Mesh hull, int origSubmeshes, HullBuilder body) {
            if (hull == null) {
                return;
            }
            Vector3[] v = hull.vertices;
            Vector2[] uv = hull.uv;
            Vector3[] nrm = hull.normals;
            bool hasUV = uv.Length == v.Length;
            bool hasNormals = nrm.Length == v.Length;
            if (!hasNormals) {
                body.DropNormals();
            }

            for (int s = 0; s < origSubmeshes && s < hull.subMeshCount; s++) {
                int[] tri = hull.GetTriangles(s);
                for (int t = 0; t < tri.Length; t += 3) {
                    int a = tri[t], b = tri[t + 1], c = tri[t + 2];
                    body.AddTri(body.skin[s], v[a], v[b], v[c],
                        hasUV ? uv[a] : default, hasUV ? uv[b] : default, hasUV ? uv[c] : default,
                        hasNormals ? nrm[a] : default, hasNormals ? nrm[b] : default, hasNormals ? nrm[c] : default);
                }
            }
        }

        /// <summary>Routes a lower hull by its cut loops: each chunk owning a closed in-window loop becomes its own piece (keyed by connected-component root) capped along that loop, with the body capped on the mirror side; every other chunk rejoins the body uncapped, welding its cut shut.</summary>
        private static void SplitLowerByLoops(Mesh hull, Mesh originalMesh, int origSubmeshes, Plane plane,
            CutContour.PlaneBounds? window, float weld, HullBuilder body, Dictionary<int, HullBuilder> pieceByRoot) {

            if (hull == null) {
                return;
            }
            Vector3[] v = hull.vertices;
            Vector2[] uv = hull.uv;
            Vector3[] nrm = hull.normals;
            bool hasUV = uv.Length == v.Length;
            bool hasNormals = nrm.Length == v.Length;
            if (!hasNormals) {
                body.DropNormals();
            }

            // 1. weld positions into stable ids so triangles that merely duplicate a vertex still share it
            float invWeld = 1.0f / Mathf.Max(weld, 1e-8f);
            var welded = new Dictionary<Vector3Int, int>();
            int[] wid = new int[v.Length];
            int weldedCount = 0;
            for (int i = 0; i < v.Length; i++) {
                var key = new Vector3Int(
                    Mathf.RoundToInt(v[i].x * invWeld),
                    Mathf.RoundToInt(v[i].y * invWeld),
                    Mathf.RoundToInt(v[i].z * invWeld));
                if (!welded.TryGetValue(key, out int id)) {
                    id = weldedCount++;
                    welded.Add(key, id);
                }
                wid[i] = id;
            }

            // 2. union welded vertices that share a skin triangle → one set per connected chunk
            int[] parent = new int[weldedCount];
            for (int i = 0; i < weldedCount; i++) {
                parent[i] = i;
            }
            for (int s = 0; s < origSubmeshes && s < hull.subMeshCount; s++) {
                int[] tri = hull.GetTriangles(s);
                for (int t = 0; t < tri.Length; t += 3) {
                    Union(parent, wid[tri[t]], wid[tri[t + 1]]);
                    Union(parent, wid[tri[t]], wid[tri[t + 2]]);
                }
            }

            // 3. one piece per chunk owning a closed in-window loop, capped per loop on both sides.
            //    Open loops (clipped by the window) seed nothing: their chunk welds back and the cut vanishes.
            if (originalMesh != null) {
                List<CutContour.Loop> loops = CutContour.ExtractLoops(originalMesh, plane, weld, window);
                for (int l = 0; l < loops.Count; l++) {
                    CutContour.Loop loop = loops[l];
                    if (!loop.closed) {
                        continue;
                    }

                    // the loop's seam vertices sit on exactly one lower chunk — that chunk is removed
                    int root = FindLoopRoot(loop.points, invWeld, welded, parent);
                    if (root < 0) {
                        Debug.LogWarning("EzySlice::SliceWindowedSplit -> closed cut loop matches no lower-hull chunk; skipping it.");
                        continue;
                    }

                    if (!Triangulator.EarClip(loop.points, plane.normal, out List<Triangle> capTris)) {
                        continue;
                    }

                    if (!pieceByRoot.TryGetValue(root, out HullBuilder pieceB)) {
                        pieceB = new HullBuilder(origSubmeshes, weld);
                        if (!hasNormals) {
                            pieceB.DropNormals();
                        }
                        pieceByRoot.Add(root, pieceB);
                    }

                    for (int t = 0; t < capTris.Count; t++) {
                        Triangle ct = capTris[t];
                        // piece cap faces up along the plane normal, body's mirror cap faces down into the gap.
                        // The body's copy is wound the other way, so its normals are the other way too --
                        // handing it the piece's would light the far side of the hole.
                        pieceB.AddTri(pieceB.cap, ct.positionA, ct.positionB, ct.positionC,
                            ct.uvA, ct.uvB, ct.uvC, ct.normalA, ct.normalB, ct.normalC);
                        body.AddTri(body.cap, ct.positionA, ct.positionC, ct.positionB,
                            ct.uvA, ct.uvC, ct.uvB, -ct.normalA, -ct.normalC, -ct.normalB);
                    }
                }
            }

            // 4. skin triangles: a seeded chunk goes to its own piece, everything else rejoins the body
            for (int s = 0; s < origSubmeshes && s < hull.subMeshCount; s++) {
                int[] tri = hull.GetTriangles(s);
                for (int t = 0; t < tri.Length; t += 3) {
                    int a = tri[t], b = tri[t + 1], c = tri[t + 2];
                    HullBuilder dst = pieceByRoot.TryGetValue(Find(parent, wid[a]), out HullBuilder pieceB) ? pieceB : body;
                    dst.AddTri(dst.skin[s], v[a], v[b], v[c],
                        hasUV ? uv[a] : default, hasUV ? uv[b] : default, hasUV ? uv[c] : default,
                        hasNormals ? nrm[a] : default, hasNormals ? nrm[b] : default, hasNormals ? nrm[c] : default);
                }
            }
        }

        /// <summary>Finds the connected-component root of the lower-hull chunk a contour loop lies on, by welding its points into the chunk vertex table.</summary>
        /// <returns><c>-1</c> when no loop point matches a lower-hull vertex.</returns>
        private static int FindLoopRoot(List<Vector3> pts, float invWeld, Dictionary<Vector3Int, int> welded, int[] parent) {
            for (int i = 0; i < pts.Count; i++) {
                var key = new Vector3Int(
                    Mathf.RoundToInt(pts[i].x * invWeld),
                    Mathf.RoundToInt(pts[i].y * invWeld),
                    Mathf.RoundToInt(pts[i].z * invWeld));
                if (welded.TryGetValue(key, out int id)) {
                    return Find(parent, id);
                }
            }
            return -1;
        }

        /// <summary>Union-find root with path halving.</summary>
        private static int Find(int[] parent, int i) {
            while (parent[i] != i) {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        /// <summary>Union-find merge of the sets containing <paramref name="a"/> and <paramref name="b"/>.</summary>
        private static void Union(int[] parent, int a, int b) {
            int ra = Find(parent, a);
            int rb = Find(parent, b);
            if (ra != rb) {
                parent[ra] = rb;
            }
        }

        /// <summary>Accumulates welded triangles into one mesh: per-skin-submesh buckets plus a trailing cap bucket, merging coincident vertices so a cut welds shut.</summary>
        /// <remarks>
        /// Two vertices merge only when position, UV and normal all agree. Position alone is not enough,
        /// and getting that wrong rewrites the whole body: an atlas-mapped mesh duplicates a vertex at every
        /// UV island border and at every hard edge, and merging those pairs drags one island's texture across
        /// its neighbour and flattens the shading — on a mesh nobody cut, since a slice rebuilds ALL of it.
        /// It also merged each cap vertex into the skin vertex underneath it, so the cut face wore whichever
        /// of the two happened to be inserted first.
        /// <para>The merge that matters still happens: a straddled triangle's upper and lower fragments get
        /// their seam position, UV and normal from one <c>GenerateUV</c>/<c>GenerateNormal</c> call each, so
        /// the two sides carry bit-identical values and rejoin. Anything that only nearly matches now stays
        /// two vertices, which costs a vertex and changes nothing on screen.</para>
        /// </remarks>
        private class HullBuilder {
            public readonly List<int>[] skin;
            public readonly List<int> cap = new List<int>();

            private readonly List<Vector3> verts = new List<Vector3>();
            private readonly List<Vector2> uvs = new List<Vector2>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly Dictionary<VertexKey, int> lookup = new Dictionary<VertexKey, int>();
            private readonly float invWeld;

            /// <summary>Whether every triangle so far arrived with a normal. Cleared by <see cref="DropNormals"/>; false makes <see cref="Build"/> recalculate the lot rather than ship a half-filled array.</summary>
            private bool haveNormals = true;

            public HullBuilder(int submeshes, float weld) {
                skin = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++) {
                    skin[i] = new List<int>();
                }
                invWeld = 1.0f / Mathf.Max(weld, 1e-8f);
            }

            /// <summary>Says a hull feeding this builder carries no normals, so the built mesh recalculates them instead.</summary>
            public void DropNormals() {
                haveNormals = false;
            }

            public void AddTri(List<int> bucket, Vector3 p0, Vector3 p1, Vector3 p2,
                Vector2 u0, Vector2 u1, Vector2 u2, Vector3 n0, Vector3 n1, Vector3 n2) {
                int i0 = Id(p0, u0, n0);
                int i1 = Id(p1, u1, n1);
                int i2 = Id(p2, u2, n2);
                if (i0 == i1 || i1 == i2 || i0 == i2) {
                    return; // welding collapsed the triangle
                }
                bucket.Add(i0);
                bucket.Add(i1);
                bucket.Add(i2);
            }

            private int Id(Vector3 p, Vector2 uv, Vector3 normal) {
                var key = new VertexKey(p, uv, normal, invWeld);

                if (lookup.TryGetValue(key, out int existing)) {
                    return existing;
                }
                int id = verts.Count;
                lookup.Add(key, id);
                verts.Add(p);
                uvs.Add(uv);
                normals.Add(normal);
                return id;
            }

            public Mesh Build() {
                if (verts.Count == 0) {
                    return null;
                }
                bool keepCap = cap.Count > 0;
                int submeshCount = skin.Length + (keepCap ? 1 : 0);

                var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = submeshCount;
                for (int i = 0; i < skin.Length; i++) {
                    mesh.SetTriangles(skin[i], i);
                }
                if (keepCap) {
                    mesh.SetTriangles(cap, skin.Length);
                }

                // the source normals, not recalculated ones: recalculating smooths every hard edge on the
                // body, which is a visible reshading of geometry the cut never touched.
                if (haveNormals) {
                    mesh.SetNormals(normals);
                } else {
                    mesh.RecalculateNormals();
                }

                // tangents are not carried through the weld, and a normal-mapped material with none renders
                // its bumps as flat dark patches. Rebuilt from the normals and UVs that are now correct.
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        /// <summary>Weld key: a vertex merges into another only when its position, UV and normal all land in the same quantized cell.</summary>
        /// <remarks>
        /// Position uses the caller's weld distance; UV and normal use fixed grids fine enough that only
        /// values meant to be equal collide. All three are quantized rather than compared exactly so the
        /// key is hashable, and identical floats always land in the same cell — which is the case the weld
        /// actually depends on.
        /// </remarks>
        private readonly struct VertexKey : System.IEquatable<VertexKey> {
            private const float UVGrid = 1e5f;      // ~1/100000 of a UV unit
            private const float NormalGrid = 1e3f;  // ~0.001 on a unit vector

            private readonly int px, py, pz;
            private readonly int u, v;
            private readonly int nx, ny, nz;

            public VertexKey(Vector3 position, Vector2 uv, Vector3 normal, float invWeld) {
                px = Mathf.RoundToInt(position.x * invWeld);
                py = Mathf.RoundToInt(position.y * invWeld);
                pz = Mathf.RoundToInt(position.z * invWeld);
                u = Mathf.RoundToInt(uv.x * UVGrid);
                v = Mathf.RoundToInt(uv.y * UVGrid);
                nx = Mathf.RoundToInt(normal.x * NormalGrid);
                ny = Mathf.RoundToInt(normal.y * NormalGrid);
                nz = Mathf.RoundToInt(normal.z * NormalGrid);
            }

            public bool Equals(VertexKey other) {
                return px == other.px && py == other.py && pz == other.pz
                    && u == other.u && v == other.v
                    && nx == other.nx && ny == other.ny && nz == other.nz;
            }

            public override bool Equals(object obj) {
                return obj is VertexKey other && Equals(other);
            }

            public override int GetHashCode() {
                unchecked {
                    int hash = px;
                    hash = hash * 397 ^ py;
                    hash = hash * 397 ^ pz;
                    hash = hash * 397 ^ u;
                    hash = hash * 397 ^ v;
                    hash = hash * 397 ^ nx;
                    hash = hash * 397 ^ ny;
                    hash = hash * 397 ^ nz;
                    return hash;
                }
            }
        }
    }
}