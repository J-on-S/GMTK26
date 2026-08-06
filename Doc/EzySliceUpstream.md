# Upstreaming the EzySlice changes

Work plan for turning the local `Assets/Script/EzySlice/` modifications into a
series of pull requests against a fork of
[DavidArayan/ezy-slice](https://github.com/DavidArayan/ezy-slice).

Nothing here is a gameplay change. The game keeps using the local copy; this is
about extracting the parts that are library work and making them presentable.

---

## 1. Ground rules

### 1.1 Comment style — match the file, not our habits

Upstream EzySlice documents exclusively with `/** ... */` block comments.
Counted in the current tree:

| File | `/** */` blocks | `///` XML lines |
| --- | --- | --- |
| `Slicer.cs` | 11 | 0 |
| `Framework/Triangulator.cs` | 4 | 9 |
| `SlicerExtensions.cs` | 4 | 36 |

`Slicer.cs` is untouched and therefore still consistent. `Triangulator.cs` and
`SlicerExtensions.cs` are now **mixed**, because our additions used `///` XML
docs — the house style for `Assets/Script/CuttingPart/`, which is correct there
and wrong here.

Rules for the upstream branch:

- **Existing files (`Triangulator.cs`, `SlicerExtensions.cs`, `Slicer.cs`):**
  convert our additions to `/** */`. A reviewer should not be able to tell which
  lines are ours from the comment syntax alone.
- **New files (`CutContour.cs`, and anything added later):** `/** */` as well.
  A new file in a library that uses one style everywhere does not get to open a
  second style.
- **Keep the substance.** The style changes; the content does not. Our comments
  document *invariants* and *why a design was chosen over the obvious
  alternative*, and that is the part worth keeping. Specifically preserve:
  - why segments are linked by mesh feature rather than by position
  - why a trimmed endpoint drops its feature key
  - why the slicer's own caps are discarded in the windowed split
  - why the bounds centre offset is baked into the matrix
  - why pieces are found by connectivity rather than proximity
- **Do not comment the obvious.** No `// increment i`. The existing upstream
  comments are sparse and explanatory; ours should read as more of the same, not
  as a different author showing up.
- **Public API gets a doc block; private helpers get one only when the
  reasoning is non-obvious.** Most of ours qualify. `Union`, `Find`, and
  `LinkOnce` do not — one line each is enough.

The local copy under `Assets/Script/EzySlice/` may keep `///` XML if we prefer
it for IDE tooltips. Only the upstream branch needs converting. If we do keep
them divergent, that divergence has to be mechanical (comment syntax only) or
future merges become miserable.

### 1.2 One idea per commit

Every item in §4 and §6 is its own commit, in the order given. No commit should
contain two unrelated changes, and no commit should leave the tree
non-compiling.

Message format, matching the repository's existing history:

```
<area>: <imperative summary under 72 chars>

<why, in prose — what problem this solves and what it does not.
 Mention any behaviour change to existing API explicitly.>
```

Reasons this matters more than usual here:

- Several changes are independently useful. `EarClip` fixes a real defect and
  can merge on its own even if everything else is rejected. Bundling it with a
  1,000-line feature guarantees it merges with nothing.
- If a maintainer objects to one piece, cherry-picking the rest has to be
  possible without untangling a blob.
- The connectivity splitter is genuinely hard to review. Reviewers need to see
  the primitives land first.

---

## 2. What is actually ours

Established by diffing against `56aabb9`, the commit that vendored the library.

**Untouched upstream:** `SlicedHull.cs`, `Framework/Plane.cs`,
`Framework/Triangle.cs`, `Framework/Line.cs`, `Framework/Intersector.cs`,
`Framework/IntersectionResult.cs`, `Framework/TextureRegion.cs`.

**Upstream, modified:**

| File | Change |
| --- | --- |
| `Framework/Triangulator.cs` | +140. `EarClip` (two overloads), `EmitEar`, `PointInTriangle`. Nothing existing altered except one trailing-whitespace fix. |
| `SlicerExtensions.cs` | +411. `WeldWithinBounds`, `WeldWhole`, `SliceWindowedSplit`, private `HullBuilder` and helpers. Nothing existing altered. |
| `Slicer.cs` | +3, **all whitespace.** No logic touched. |

**New:** `CutContour.cs` (511 lines). A deleted `CutContourAuthoring.cs` (184
lines) also existed at one point and is not part of this.

The important structural fact: **the slicer itself was never modified.**
Everything we built either reads geometry, fixes triangulation, or
post-processes hull output. That is what makes these changes upstreamable at
all, and it is a property to preserve — no PR in this series should touch
`Slicer.Slice`'s existing code path.

---

## 3. Decisions to settle before writing code

### 3.1 Should contour points carry normals and other attributes?

**Yes, opt-in, as parallel lists.**

The criterion is *what the caller loses by receiving only positions*. Anything
derivable from the ordered points plus the plane normal should not be stored:

| Attribute | Verdict | Why |
| --- | --- | --- |
| Surface normal at the point | **Offer** | Interpolated from the crossed edge's vertex normals. Unrecoverable once the mesh association is gone. Wanted for decals, wound particles, blood spawning, aligning a ribbon to the surface. |
| UV | **Offer** | Same argument. Needed to texture anything placed along the contour. |
| Tangent | **Offer** | Same argument. Only matters for normal-mapped decals, so lowest priority of the three. |
| Source feature key (crossed edge / ON vertex) | **Offer** | Already computed internally at zero extra cost. Lets advanced callers map a contour point back to mesh topology. |
| In-plane outward normal (perpendicular to the loop, in the cut plane) | **Do not store** | This is what `Bisector` computes. Fully determined by the neighbouring points and the plane normal. |
| Arc length / normalised `t` along the loop | **Do not store** | Trivially derived. Cheap to add as a helper if wanted. |
| Curvature | **Do not store** | Same. |

Why opt-in rather than always: `mesh.normals`, `mesh.uv`, and `mesh.tangents`
each **allocate a fresh managed array on every property access** in Unity.
Reading all three roughly quadruples the extraction's allocation cost, and the
common case — draw the path — needs none of them.

Why parallel lists rather than a fat point struct: the common case wants
positions only, and a struct-of-arrays layout means that case pays for
positions only. It also matches Unity's own `Mesh` API convention, which
reviewers will recognise.

Proposed shape:

```csharp
[System.Flags]
public enum ContourAttributes {
    None     = 0,
    Normals  = 1 << 0,
    UVs      = 1 << 1,
    Tangents = 1 << 2,
    Features = 1 << 3,
}

public struct Loop {
    public List<Vector3> points;     // always present
    public bool          closed;

    public List<Vector3> normals;    // null unless requested
    public List<Vector2> uvs;        // null unless requested
    public List<Vector4> tangents;   // null unless requested
    public List<long>    features;   // null unless requested
}
```

Invariant to document and hold: **every non-null list has the same count as
`points`, and index `i` describes the same contour point in all of them.**

Two implementation notes:

- Interpolation needs the crossing parameter `t` along the edge, which
  `Intersector.Intersect` currently computes and discards. It needs an overload
  that outputs `t`. That is an additive change to an upstream file — its own
  commit, and it must not alter the existing overload's behaviour.
- A point on an **ON vertex** (rather than a crossed edge) takes that vertex's
  attributes directly, no interpolation. A point produced by **window clipping**
  sits between two crossings and needs its attributes lerped along the original
  segment — easy to get wrong, worth an explicit comment.

### 3.2 Local or world coordinates?

**Mesh-local. Always. No option, no flag.**

Four reasons, strongest last:

1. The mesh is authored in local space and extraction happens there. Converting
   is an extra O(n) pass the caller may not want.
2. World-space results go stale the instant the object moves. Local ones stay
   valid for the lifetime of the mesh.
3. Cached contours (the guide loop, the gizmo preview) want to survive the body
   being moved, re-parented, or scaled.
4. **The `Mesh` overload has no transform and therefore cannot return world
   space at all.** Offering world space on the `GameObject` overload would mean
   two overloads returning geometrically different results behind the same type.
   That is a trap, not a convenience.

The current implementation is already local and already says so
(`CutContour.cs:9`). Keep it, and state it in the class-level doc block, on the
`Loop` struct, and on every public method that returns points — this is the kind
of thing people get wrong once and then debug for an hour.

Ship an explicit converter so the call site shows the conversion:

```csharp
/** Converts contour points from mesh-local space into world space. */
public static void ToWorld(List<Vector3> localPoints, Transform t, List<Vector3> dst);

/** Converts contour normals into world space, using the inverse transpose so
 *  non-uniform scale does not skew them off the surface. */
public static void NormalsToWorld(List<Vector3> localNormals, Transform t, List<Vector3> dst);
```

The separate normal converter is not pedantry. Normals transform by the inverse
transpose, not the matrix — `ExtractLoops(GameObject, ...)` already does that
dance going the other direction (`CutContour.cs:296-299`), and the symmetric
case needs the same care or non-uniformly-scaled objects produce normals that
are visibly wrong.

---

## 4. Work items

Ordered. Each is one commit unless stated. Each compiles and is useful alone.

### 4.1 Revert the `Slicer.cs` whitespace

Three lines of stray whitespace. Removing them means the PR series shows
`Slicer.cs` as untouched, which is a claim worth being able to make.

Not a commit of its own — fold into the branch setup, or simply never carry it
across.

### 4.2 `Triangulator.EarClip`

**Files:** `Framework/Triangulator.cs`

Ear-clipping triangulation for a single closed planar contour. Replaces nothing;
`MonotoneChain` stays exactly as it is.

Rationale for the commit message: upstream's only triangulator is a convex hull,
which fills concavities and bridges disjoint cross-sections with phantom
geometry. `EarClip` caps one contour exactly.

Work:

- Convert doc comments from `///` to `/** */`.
- Replace the basis-degeneracy check `if (Vector3.zero == u)` with an explicit
  `if (u.sqrMagnitude < 1e-12f)`. The current form works — `Vector3.==` has a
  built-in epsilon — but relying on that is accidental.
- Add a doc note that holes are not supported (see §6.3), so nobody discovers it
  by shipping a torus.
- Keep both overloads (with and without `TextureRegion`), matching
  `MonotoneChain`'s existing shape.

Known limitations to state in the doc block, not hide:
- No hole support. Nested contours cap solid.
- The `widest`/`fallback` path clips the least-bad corner when no valid ear
  exists, so malformed input produces wrong geometry rather than hanging. Say
  so.

### 4.3 `Intersector.Intersect` overload returning `t`

**Files:** `Framework/Intersector.cs`

Purely additive. The existing overload keeps its signature and behaviour and is
reimplemented in terms of the new one.

Needed by 4.5. Landing it separately keeps 4.5's diff about contour extraction
rather than about plumbing.

### 4.3b `Plane` — fix the three-point constructor, add `Flipped`

**Files:** `Framework/Plane.cs`

**A genuine upstream bug.** The three constructors disagree on the sign of
`m_dist`:

```csharp
Plane(Vector3 pos, Vector3 norm)        →  m_dist =  Dot(norm, pos)   // :36
Plane(Vector3 norm, float dot)          →  m_dist =  dot              // :45
Plane(Vector3 a, Vector3 b, Vector3 c)  →  m_dist = -Dot(normal, a)   // :56
```

`SideOf(pt)` is `Dot(normal, pt) - m_dist`. For the point-normal constructor,
`SideOf(pos)` is `Dot(n,pos) - Dot(n,pos) = 0` → `ON`, correct. For the
three-point constructor, `SideOf(a)` is `Dot(n,a) - (-Dot(n,a))` = `2·Dot(n,a)`,
which is only zero when the triangle passes through the world origin.

So a plane built from three points does not contain those points. It sits at
twice its distance from the origin, on the wrong side.

Nothing in the current tree calls it, which is presumably why it has survived.
It matters for §6.2: building a convex cutter's face planes from the cutter
mesh's triangles is the obvious approach, and it would silently place the
volume somewhere else entirely.

Fix is one character. Ship it with a test asserting `SideOf` returns `ON` for
all three input points, on a triangle deliberately not at the origin.

Add in the same commit:

```csharp
/** The same plane with UP and DOWN swapped. Both the normal and the distance
 *  invert — flipping only the normal moves the plane rather than reversing it. */
public Plane Flipped() {
    return new Plane(-m_normal, -m_dist);
}
```

This is what lets callers choose which side of a cut comes off (see 4.7),
without any API needing a side parameter. The doc comment earns its place: the
`dist` half is exactly the part people get wrong.

### 4.4 `CutContour` — positions only

**Files:** new `CutContour.cs`

Ordered contour extraction. Positions only in this commit; attributes arrive in
4.5. `PlaneBounds` arrives in 4.6.

Work:

- Convert all doc comments to `/** */`.
- **Drop `Ribbon`, `Bisector`, `ScaleLoop`, `GetCenter`.** They are the game's
  guide-rendering helpers, not contour extraction. Move them to
  `Assets/Script/CuttingPart/` in a separate local-only commit.
- Take a `Mesh`, not a `GameObject`, in the primary overload. Keep a
  `GameObject` convenience overload that resolves the `MeshFilter` and
  transforms the plane, since that is what most callers have.
- Document the local-space invariant in three places per §3.2.
- Document the degree-≤2 precondition on `WalkLoop`. Non-manifold input can
  produce a degree-3 node, in which case the walk silently takes the first
  unvisited neighbour and drops the branch. Either detect and warn, or state it
  as a precondition — but do not leave it implicit.

### 4.5 `CutContour` attributes

**Files:** `CutContour.cs`

Adds `ContourAttributes`, the optional parallel lists, and the interpolation, per
§3.1. Depends on 4.3.

Document the parallel-list invariant. Handle the three point origins distinctly:
crossed edge (interpolate by `t`), ON vertex (copy directly), clipped endpoint
(lerp along the original segment).

### 4.6 `PlaneBounds`

**Files:** `CutContour.cs`

The finite rectangular window and its Liang-Barsky clip, plus the open-chain
behaviour it produces.

Work:

- `BuildBounds` currently returns `PlaneBounds?` but has no path that returns
  null. Return `PlaneBounds` — the "no window" case belongs on the *parameter*,
  not the return type.
- Take a `Matrix4x4` or explicit basis rather than a `Transform`, so the type
  is usable without a scene. Keep a `Transform` overload for convenience.
- Document the mechanism explicitly: a clipped endpoint drops its feature key,
  so it cannot chain, so the contour comes back open. That is the design, and it
  reads as an accident if unexplained.

### 4.7 `SliceConnected`

**Files:** new file in `SlicerExtensions.cs` or a new `SlicerConnected.cs`

Connectivity-based splitting: union-find over shared vertices, one output mesh
per physically-joined chunk.

Cannot be an overload of `Slice` — the return type changes from one
`SlicedHull` to N results, and a parameter cannot change a return type. New
name, and `Slice` stays the default.

**Which side comes off is already caller-controlled — do not add a side
parameter.** Negating the plane (`new Plane(-pl.normal, -pl.dist)`) swaps UP and
DOWN and therefore swaps which hull is treated as the body and which is
searched for pieces. The split is symmetric under that flip, including the part
that looks like it would not be: `EarClip` winds its caps to face whatever
normal it is handed, the piece takes that winding and the body takes it
reversed, so both flip together and each cut face still points the right way.
`ExtractLoops` is unaffected — `Crosses` tests UP/DOWN against DOWN/UP, which is
symmetric — and `EarClip` re-orients by shoelace regardless.

A side parameter would duplicate what the plane already expresses. Document the
negation instead, and add the helper in 4.3b so callers do not have to remember
that `dist` flips too.

This is cleanest when the API takes a `Plane`. Flipping a `Transform`-based
overload means rotating the object 180°, which drags the window's U/V axes with
it — one more reason the primary overloads take `Plane` and `Mesh`, with
`Transform` and `GameObject` as convenience wrappers.

Work before it can go up:

- **Decouple from the window.** Connectivity splitting is useful with no window
  at all — slicing a barbell should give three objects, not two hulls, and that
  is a standing request against upstream. Our version fuses the two because the
  game always needs both. Split them.
- Take a `Mesh`, not a `GameObject`.
- Document that caps are rebuilt per contour rather than reused, and why: the
  slicer's cap is a single convex hull spanning every cross-section at once, so
  it both bridges disjoint loops and — being a fan from one vertex — cannot be
  attributed to a chunk.

### 4.8 Bounded slice, done properly

**Files:** `Slicer.cs` — the only commit in the series that touches it

A `Slice` overload taking bounds, rejecting **during** the cut rather than
repairing afterwards.

Our local `WeldWithinBounds` approach — slice with the infinite plane, then glue
shut everything outside the window — is correct for the game and wrong for a
library:

- an extra full vertex pass over both hulls
- `RecalculateNormals()` over the whole mesh, softening every hard edge the
  plane never approached
- position-welding collapses UV seams across the entire outside-window region

Invisible on organic meshes, destructive on hard-surface ones.

The proper version tests the window at triangle classification: a triangle that
straddles the plane but lies outside the window is emitted whole to one side and
never split. Untouched triangles keep their normals, UVs, and tangents exactly.
No repair pass, and it is *faster* than an unbounded slice rather than slower.

This is the largest single piece of work in the series and the only one that
requires understanding `Slicer`'s internals. Consider deferring it past the
first PR round.

### 4.9 What does not go upstream

| Thing | Reason |
| --- | --- |
| `WeldWithinBounds`, `WeldWhole` | Built around our "cross-section material must be null so the cap lands in a trailing submesh" convention (`CuttableObject.cs:85-92`) and around meshes that want smooth shading. Game semantics. |
| `Ribbon`, `Bisector`, `ScaleLoop`, `GetCenter` | Guide rendering. Move to `Assets/Script/CuttingPart/`. |
| `CuttableObject`, `CutPlane` as they exist | Welded to the game — `SavedLoop`, `ApplyMaterials`, `CenterPivot`, `SpawnPiece`, the two-phase pending hull. See §5 for what a clean version looks like. |

---

## 5. The authoring and editor layer

**Verdict: yes, build it — but as a separate optional layer, and fresh rather
than ported.**

### 5.1 Why it is worth doing

- A bounded, connectivity-aware slicer is close to unreviewable without
  something clickable. A maintainer with no test scene has to take our word for
  every claim in the PR.
- It is the manual test harness the library does not currently have, and that
  we do not currently have either.
- It doubles as documentation. "Drop this component on a cube, drag the plane,
  press Cut" is a better README section than any prose.

### 5.2 Why it must be separated

Upstream is pure static extension methods with zero `MonoBehaviour`s. Dropping
components into the same folder changes the library's character, and the moment
anyone adds an assembly definition, a `MonoBehaviour` plus its custom editor
drags a `UnityEditor` reference into a runtime assembly and breaks player
builds.

```
EzySlice/
  Runtime/     — Slicer, SlicedHull, SlicerExtensions, CutContour, Framework/
  Authoring/   — MonoBehaviours. Depends on Runtime. Ships in builds.
  Editor/      — Custom editors and gizmos. Editor-platform asmdef only.
  Samples/     — A scene with a few meshes set up. Optional.
```

If upstream has no asmdefs, propose the folder split anyway and let the
maintainer decide. Do not put an editor script next to `Slicer.cs`.

### 5.3 `SliceAuthor` (the plane component)

Same idea as our `CutPlane`, stripped of game specifics: the transform is the
plane, its up axis is the normal, and a `BoxCollider` (kept disabled) is the
window handle.

The disabled-collider trick from `CutPlane.cs:116-146` is worth carrying over
along with its reasoning — an enabled window collider sits between the player's
aim and the target and swallows the interaction raycast. That is a real trap and
the `OnValidate` that switches it back off, loudly, is a good pattern.

Keep out: `CuttableObject` targeting, `SavedLoop`, `previewScale`, the
`GizmoUtils` dependency, and the live per-frame re-extraction. Re-extracting the
contour every editor frame (`CutPlane.cs:169-170`) is fine for one small body
and will lock the scene view on anything larger — preview on drag-release
instead, or behind an explicit toggle that defaults off.

### 5.4 `SliceTarget` (the cuttable component) and its inspector

A thin component holding the tuning the library needs — weld distance,
cross-section material, whether to split by connectivity — and nothing else. No
pending state, no two-phase commit, no piece spawning policy. Those are game
decisions.

Inspector buttons, in rough order of value:

| Button | What it does | Why it earns its place |
| --- | --- | --- |
| **Validate** | Runs every precondition and reports as help boxes: does the plane intersect the mesh, how many loops, how many are closed, is there a `MeshFilter` with a readable mesh, **will the cross-section material collide with an existing skin submesh**, is a supplied cutter convex. | The single most valuable one, and the least obvious. The cross-section material trap in particular currently costs an hour to diagnose from symptoms. |
| **Cut** | Performs the slice and applies the result. | The obvious one. |
| **Copy Lower Hull** / **Copy Upper Hull** | Spawns a hull as a sibling `GameObject` without modifying the original. | Lets you inspect exactly what the slicer produced, independent of any reassembly. This is the one asked for by name, and it is genuinely the best debugging tool in the list. |
| **Save Mesh Asset** | Writes the result into `Assets/` as a `.asset`. | Runtime-generated meshes vanish on domain reload and cannot be opened in the Project window. Everyone needs this and almost nobody has it. |
| **Restore Original** | Puts the pre-cut `sharedMesh` back. | `Undo.RecordObject` covers one step; an explicit restore survives many cuts and a domain reload. |
| **Refresh Preview** | Extracts and draws contours without cutting. | The expensive part of authoring. Explicit, not per-frame. |
| **Stats** | Triangle and vertex counts before/after, loop count, closed vs open, piece count, elapsed ms. | Cheap to add, and it is exactly what a reviewer wants to see in a screenshot. |
| **Stress: cut N times** | Repeated randomised cuts. | Demonstrates the geometry-growth behaviour honestly, and catches robustness bugs that one careful cut never will. |

`Copy Lower Hull` deserves a note: it should copy **without** any welding,
windowing, or connectivity processing — raw slicer output. Its whole purpose is
to answer "did the slicer do this, or did our post-processing do this," and it
can only answer that if it skips the post-processing.

### 5.5 Commit split for this layer

Its own commits, after the runtime work, and ideally its own PR:

1. Folder restructure and asmdefs (if proposing them).
2. `SliceAuthor` plus its gizmo.
3. `SliceTarget` plus the basic inspector (Cut, Copy hulls, Restore).
4. Validation and stats.
5. Sample scene.

---

## 6. Future work

Not part of the first PR series. Recorded so the design is not re-derived later.

### 6.1 Tier 1 — arbitrary window shape

Today the window is a rectangle. It could be any 2D outline: a polygon, a blob,
a hand-drawn curve.

The cut stays **flat**. This changes the outline of the cut region, not its
depth. A star-shaped hole through a wall, yes; a scooped dent, no.

Cheap because the entire architecture routes through one question — *is this
point inside the window?* Replace the rectangle test with a general
point-in-polygon test and everything downstream is untouched: the clipping, the
open-chain behaviour, the "incomplete cut does nothing" rule, the connectivity
pass.

Work:

- Generalise `PlaneBounds` to `IPlaneWindow` with `Contains(Vector2)` and
  `ClipSegment(...)`, keeping the rectangle as the fast default implementation.
- Convex polygon window: Sutherland-Hodgman clip. Straightforward.
- Arbitrary (concave) polygon window: a segment can enter and leave multiple
  times, so `ClipSegment` must return a *list* of sub-segments rather than one.
  That changes the caller loop and is the only real complication.

Best folded into 4.8 — if the window is being tested inside the slicer anyway,
making it a general shape at the same time is marginal extra cost.

### 6.2 Tier 2 — convex volume cutting (`CutMesh`)

Cut by a closed **convex** volume — box, wedge, cone, prism — removing exactly
what is inside it.

The key observation: a convex shape is the intersection of half-spaces, and a
half-space is what EzySlice already cuts with. A box is six planes. So cutting
by a convex volume is slicing repeatedly, once per face, keeping the inside
each time. What survives every slice is the removed piece; everything set aside
along the way is the body.

Nothing in the slicing math changes. It is `Slice` called k times.

**Semantic warning worth stating in the docs:** this is not a fancier
`CutPlane`. A plane cut with a window removes *everything past the plane* — cut
at the wrist, the whole hand comes off. A volume cut removes *exactly the volume
drawn* — cut a box at the wrist and you carve a slot, leaving the hand floating.
Severing versus scooping. Different verbs, both useful, not interchangeable.

The hard part is **internal face cancellation**. Each of the k slices leaves a
cut face. Some are real wound surface; some are internal walls between two body
fragments and must disappear. The rule: an internal face is one where the same
triangle appears twice with opposite winding. Cancel back-to-back pairs; what
remains is real surface. Sound rule, fiddly under floating point.

Other requirements:

- **Convexity must be checked and refused**, loudly, in `OnValidate` and at the
  API boundary — the same way `CutPlane.WindowSize` fails closed on a missing
  box rather than silently cutting the body in half. A concave cutter produces
  silent garbage, which is the worst failure mode to ship.
- Face extraction: dedupe the cutter mesh's triangles to unique planes (a box
  mesh is 12 triangles, 6 planes), orient every normal outward, transform into
  the target's mesh-local space. **This is what needs 4.3b** — building those
  planes from triangles is the obvious approach, and the unfixed three-point
  constructor would put every face plane in the wrong place, silently.
  Orienting outward is `Flipped()` on any face whose normal points inward.
- Removing a chunk can **disconnect** the remaining mesh — carve through a wrist
  and the hand is now loose. Re-run connectivity after the carve and treat
  orphans as pieces. Reuses 4.7.
- **Do not preview live.** Six-plus full slices per editor frame will make the
  scene view unusable. Draw the volume's wireframe live; compute the result on
  demand.

Dependencies: needs 4.2 (carved faces are frequently concave), 4.4 (caps come
from contours), and 4.7 (orphan detection). Which is the strongest argument for
landing those first — the volume cutter then arrives as a small, obvious
composition of things already merged, rather than as a wall of new code.

Ships as a new file calling `Slice` repeatedly. `Slicer.Slice` itself stays a
single-plane primitive. That discipline is what makes the whole series
reviewable.

### 6.3 Holes in `EarClip`

`EarClip` fills one outline. It has no concept of one outline being a hole in
another, so a hollow cross-section — a bone with a marrow channel, a tube, any
ring shape — caps solid.

`ExtractLoops` already returns both the outer and inner contours, so the
information is present; the triangulator just cannot use it.

Standard fix is a **keyhole cut**: before triangulating, join the inner contour
to the outer with a thin corridor, producing a single outline that runs out
along one side of the corridor and back along the other. Ear clipping then
handles it as one shape and the hole stays open.

Worth doing eventually. It is both the first thing a library reviewer asks about
and a real limitation the game hits the moment a body part has a hollow bone.

### 6.4 Tier 3 — arbitrary mesh cutting

Explicitly **out of scope**, recorded so it is not proposed again.

Cutting by an arbitrary (possibly concave, possibly hollow) mesh is a boolean
mesh operation. The failure modes — coplanar faces, self-intersection,
near-degenerate triangles, meshes that are not quite closed — are why libraries
like CGAL, libigl, Cork, and Carve each represent years of work and still carry
open bugs.

More decisively: **none of EzySlice would survive it.** Every part of the
library assumes a plane — classify each vertex above or below, split the
triangle where it crosses, cap with one flat polygon. Booleans need a different
engine underneath. This would not be extending EzySlice; it would be replacing
it and keeping the folder name.

If it is ever genuinely needed, integrate an existing library.

---

## 7. Commit order

Send 1 on its own, first and immediately. It is a one-character bug fix with a
test, it is unrelated to everything else, and merging it establishes that the
series is worth reading.

Runtime, first PR:

1. `plane: fix dist sign in the three-point constructor, add Flipped`
2. `intersector: add Intersect overload reporting the crossing parameter`
3. `triangulator: add EarClip for exact single-contour capping`
4. `contour: add CutContour for ordered cut-outline extraction`
5. `contour: add optional per-point normals, UVs, tangents and features`
6. `contour: add PlaneBounds for finite windows on the cutting plane`
7. `slicer: add SliceConnected splitting output by mesh connectivity`

Runtime, second PR (larger, may want its own discussion first):

8. `slicer: add bounded Slice overload rejecting during the cut`
9. `slicer: generalise the cut window to arbitrary convex outlines`

Authoring, separate PR:

10. `restructure into Runtime/Authoring/Editor folders`
11. `authoring: add SliceAuthor plane component and gizmo`
12. `editor: add SliceTarget inspector with cut and hull-copy actions`
13. `editor: add validation and slice statistics`
14. `samples: add a demo scene`

Local-only, not upstreamed — do these in the game repo so the two trees do not
drift for no reason:

- move `Ribbon`, `Bisector`, `ScaleLoop`, `GetCenter` out of `CutContour.cs`
  into `Assets/Script/CuttingPart/`
- drop the `Slicer.cs` whitespace

---

## 8. Before opening the PR

- [ ] `Slicer.cs` shows as unmodified in commits 1-6.
- [ ] No `///` XML doc comments anywhere on the upstream branch.
- [ ] Every public method takes `Mesh` where it can, `GameObject` only as a
      convenience overload.
- [ ] Every method returning points states "mesh-local space" in its doc block.
- [ ] No commit leaves the tree non-compiling.
- [ ] `EarClip`'s hole limitation is documented, not hidden.
- [ ] `Plane(a, b, c)` has a test proving `SideOf` returns `ON` for all three
      input points, on a triangle away from the origin.
- [ ] `SliceConnected` documents the `Flipped()` negation as the way to choose
      which side comes off, and takes no side parameter.
- [ ] Trailing newline at end of `SlicerExtensions.cs` (currently missing).
- [ ] At least three manual test cases exercised and screenshotted: a plane
      through a torus (multiple loops), through a two-pronged fork
      (connectivity), and a window clipped mid-loop (open chain).
- [ ] README section covering the new entry points, with the local-space
      invariant stated once, prominently.
