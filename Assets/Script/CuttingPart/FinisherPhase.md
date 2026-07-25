# Finisher phase — design note

Status: **implemented, not yet run.** Nothing here has been compiled or played — Unity has
to confirm it. This is the beat between "the player closed the loop" and "the mesh is
actually sliced".

Files: `CutFinisher.cs`, `Editor/FinisherPreview.cs`, `Editor/CutFinisherEditor.cs`,
`Editor/EditorCameraClaim.cs`, plus changes in `CuttingManager.cs`, `CutRegionHighlighter.cs`,
`Editor/CutPreview.cs`, `Editor/CuttingManagerEditor.cs`.

## Setting one up

1. Add a `CutFinisher` to the cut (the object with the `CuttingManager`, or a child of it).
2. Auto-wire on the manager, or drag it into the manager's **Finisher** slot.
3. Navigate the Scene view to the shot you want, then **Grab from Scene view**. It is stored
   in the body's local space; nudge it afterwards with the Scene view handles.
4. Assign the **Tool Prefab** — per cut, not looked up from `Required Tool Name`.
5. **Preview the finisher** to check the framing and the swing. Nothing is sliced.

Leaving the slot empty, or unticking **Enable Finisher**, gives exactly the old behaviour:
splice and quit the frame progress hits 1.

## What the player sees

The tracing minigame ends when `currentProgress` reaches 1. Today the mesh is spliced on
that exact frame and the rig quits — the cut just happens, with no punctuation.

Instead:

1. The camera leaves the orbit and moves to a framed close-up of the body part.
2. A tool (bone saw, cleaver — whatever the cut calls for) hovers over the part, bobbing.
3. The player clicks.
4. The tool slashes down through the cutting plane. The splice fires at the moment the
   blade crosses the plane, hidden by the blade itself.
5. The severed piece is kicked away, the camera flies back out.

The point is that the splice — a single frame where geometry pops — lands underneath a
motion the player initiated, instead of happening on its own.

## Where it sits in the state machine

`CuttingManager` already carries two independent enums, and this adds to the second:

- `CuttingState` — **how far the cut got.** `NOT_STARTED` → `PROGRESSING` → `COMPLETED`.
- `RigPhase` — **where the camera is.** `Free` → `Entering` → `Cutting` → `Exiting` → `Free`.

The finisher is a new `RigPhase.Finishing`, entered from `Cutting` and left into `Exiting`:

```
Free ──EnterMinigame──▶ Entering ──▶ Cutting ──progress>=1──▶ Finishing ──click+slash──▶ Exiting ──▶ Free
                                        └──────────────Q──────────────────────────────────┘
```

Q-to-quit stays available only in `Cutting`. Once the loop is closed the run is won; there
is no half-spliced state to back out of.

## CuttingManager changes

`Update`'s `Cutting` case currently calls `HandleCompletion()` the instant progress hits 1,
and `HandleCompletion` both splices and quits. Split that:

```csharp
case RigPhase.Cutting:
    ...
    if (currentProgress >= 1) BeginFinisher();
    else if (Keyboard.current.qKey.wasPressedThisFrame) QuitMinigame();
    break;

case RigPhase.Finishing:
    // the finisher owns the camera and the tool; nothing to tick here
    break;
```

`BeginFinisher` parks the cutting rig without restoring the camera, then hands off. Note it
does **not** call `ReleaseCamera` — the finisher flies the camera to its shot from wherever
the orbit left it, and the free-look snapshot `CompleteExit` restores is still the one taken
on entry.

**`HandleCompletion` splits in two, not one.** The original body — splice, quit, fire the
event — cannot all happen on the impact frame: the splice has to land under the blade, but
the camera must not start flying out until the follow-through is finished, and those are
different frames. So:

```csharp
void HandleCompletion()
{
    if (finisher != null && finisher.CanRun) { BeginFinisher(); return; }
    ApplySplice();   // ← what used to be the first half
    FinishUp();      // ← and the second
}

void ApplySplice()  // fired by the finisher on the impact frame
{
    state = COMPLETED; StopCutSound(); play tear;
    LastSeveredPiece = SliceOffPart();
}

void FinishUp()     // fired by the finisher after the follow-through
{
    QuitMinigame();
    OnMinigameCompleted?.Invoke(this, LastSeveredPiece);
}
```

The no-finisher path calls them back to back, so it is byte-for-byte the old behaviour.
`CutFinisher.Begin(onImpact, onDone)` takes both.

`LastSeveredPiece` is a new public property on the manager: the finisher needs the piece to
kick it, and it does not exist until `ApplySplice` returns.

**Guards.** `QuitMinigame` guarded on `isPlaying` (`phase == Cutting`), which the finisher's
hand-back would fail. Added `inMinigame` (`Cutting || Finishing`); `QuitMinigame` and
`EnterMinigame` use it, `UpdateCutSound` keeps the narrow `isPlaying` — the loop must not
sound through the close-up.

## CutFinisher component

New MonoBehaviour, owns all of the presentation. `CuttingManager` knows only `Begin` and the
callback.

Split the same way `CuttingManager` / `CutMinigamePreset` are: shareable feel in an asset,
per-shot things on the component.

**On the component** — what cannot live in an asset, plus the opt-out:

```csharp
public bool enableFinisher = true;  // off = splice and quit, as before this existed
public CutFinisherPreset preset;    // assign it and every inline number is ignored
public CuttingManager manager;      // resolved from the hierarchy

public bool hasShot;                // framed yet? distinct from "framed at the origin"
public Vector3 shotLocalPosition;   // IN THE BODY'S LOCAL SPACE
public Vector3 shotLocalEuler;
```

**The shot is stored in the cuttable's local space, not world.** The body gets carried,
rotated and dropped on a table; a world-space shot would be framing empty air the moment it
moves. `ShotSpace` is `Manager.GameObjectBeingCut.transform` (falling back to the finisher's
own transform while the cut is half-wired, so a shot framed early isn't silently lost to
world space), and `TryGetCameraPose` resolves through `TransformPoint` — which carries the
body's scale, so rescaling the body doesn't leave the camera at the wrong distance.

There is no proxy GameObject for the shot. **Grab from Scene view** stores wherever you are
looking from, and `OnSceneGUI` puts move/rotate handles on it, writing back through
`SetShotFromWorld`.

**In `CutFinisherPreset`** — everything else, each with an inline fallback on the component
for finishers authored before the asset existed:

```csharp
public float cameraFOV = 40f;       // close-ups want tighter than the cut's own
public float easeIn = 0.5f;         // seconds to reach cameraPose
public AnimationCurve easeInCurve;

public GameObject toolPrefab;       // WIRED PER CUT -- see below
public Vector3 toolEuler;           // for prefabs whose blade isn't down +Z

public float sweepAngle = 0f;       // around the cut normal, -180..180
public float approachTilt = 90f;    // out of the cutting plane, -90..90

public float bobAmp = 0.06f, bobHz = 1.5f;
public float autoSlashAfter = 0f;   // 0 = wait forever

public float hoverHeight = 0.25f;   // where the swing starts, and where the tool waits
public float sweepDist = 0.6f;      // half-travel across the cut
public float slashTime = 0.18f;
public AnimationCurve slashEase;
public float holdAfter = 0.25f;     // beat on the aftermath before flying out
public float kick = 3f;             // impulse on the severed piece
```

The shot is deliberately not in the preset: it is framed per body part, in that body's own
space, so carrying it to another cut would point the camera at the wrong limb.

**The tool waits at the swing's own start** — the `t = 0` end of the slash, bobbing along the
approach axis from there. Not at a separate offset: anywhere else and the blade would jump to
the swing start the instant the player clicks. So `hoverHeight` sets both, and there is one
number rather than two that have to agree.

`bobAmp` defaults to 0.06 against a 0.25 hover. The original 0.02 was an 8% wobble, which is
why the bob read as doing nothing.

**The tool prefab is wired per cut, not looked up from `requiredToolName`.** What the player
must be *holding* to start the cut and what *swings* at the end are separate decisions — a
cut that needs a scalpel in hand can still finish with a bone saw — and a string-to-prefab
lookup would need a registry to resolve against.

**`enableFinisher`** is the opt-out. Off, or with no finisher on the cut at all,
`HandleCompletion` takes the direct path and the cut behaves exactly as it did before.

Author `cameraPose` as a Transform placed by hand in the scene rather than computing an
offset. Framing a chop is a shot composition problem; you want to eyeball it per body part.
The editor preview below is what makes that eyeballing cheap.

### Frame basis

Everything derives from this cut's `CutPlane` — `CuttingManager.CutPlane`, which is
`loopGuide.plane`. A body can carry several, so the finisher must read the one its own
manager is running, never "the body's plane".

**The sweep is a free axis, and the approach follows it.** Two angles, no enum — which way a
limb wants to be chopped depends on how the limb is lying, and that is not a choice between
four named options.

```csharp
sweep    = plane.right * cos(sweepAngle) + plane.forward * sin(sweepAngle)   // ⟂ normal, always
inPlane  = cross(plane.normal, sweep)                                        // ⟂ sweep, in-plane
approach = inPlane * cos(approachTilt) + plane.normal * sin(approachTilt)     // ⟂ sweep, always
```

`sweepAngle` picks **any** axis perpendicular to the cut normal. `approachTilt` then decides
how far out of the cutting plane the blade comes in:

| `approachTilt` | what it looks like |
|---|---|
| `0` | blade comes in sideways and stays in the cutting plane — an actual chop across the limb |
| `90` | blade drops straight down the cut normal — *along* the limb, rarely what you want |
| `-90` | the same, from underneath |

Perpendicularity is structural, not something to keep in step by hand: both axes `approach`
is blended from are already perpendicular to `sweep`, so every tilt is too.

Defaults are `sweepAngle 0`, `approachTilt 90`, which reproduces the original fixed
down-the-normal behaviour. The kick reuses `-ApproachAxis`, so the piece is pushed the way
the blade was going.

`CutCenter` is `LoopGuideBuilder.LoopCenter` when the guide has a loop, falling back to
`plane.Origin` — the plane origin can sit well off to one side of the ring it cuts, which
would put the chop off-centre.

`OnDrawGizmosSelected` draws the approach line, the bob sphere, the sweep axis, the swing and
the shot, so the angles are checkable without starting a preview.

### Split the pose maths out of the coroutine

This is the one structural rule that matters, and it is the same one `CameraFollow` already
follows: `TryGetPose` computes, `ApplyPose` writes, and only the second one is allowed to
have side effects. Do the same here, or the editor preview below cannot exist without a
second copy of the maths that will drift.

```csharp
public const float ImpactT = 0.5f;   // the t at which the blade reaches the cut

// pure: no side effects, no dependence on Time, callable in edit mode
public bool TryGetToolPose(float t, float clock, out Vector3 position, out Quaternion rotation);
public bool TryGetCameraPose(out Vector3 position, out Quaternion rotation, out float fov);
public Vector3 SeveredOffsetAt(float t);
```

`t` is the whole beat parameterised: below 0 the tool waits, 0→1 is the slash. `clock` drives
the bob and is passed in rather than read from `Time.time`, since edit mode has no running
clock. The coroutine becomes a driver that walks `t` and calls the writers; the preview is a
second driver over the same `t`.

### The coroutine

1. **Ease in.** Lerp `sceneCamera` position/rotation/FOV to `TryGetCameraPose` over `easeIn`.
   Nothing needs saving — `CuttingManager.initialCameraPos/Rot/FOV` already hold the
   free-look snapshot that `CompleteExit` restores.

2. **Wait.** `TryGetToolPose(t < 0, clock)` parks the tool at the swing's start, `from` below,
   plus `approach * sin(clock * bobHz * TAU) * bobAmp`. Oriented `LookRotation(sweep,
   approach)`, so a prefab modelled edge-down chops the way it looks like it should. Loops
   until `Mouse.current.leftButton.wasPressedThisFrame`, or `autoSlashAfter` elapses.

3. **Slash.** Lerp `t` 0→1 over `slashTime` through `slashEase`. `TryGetToolPose` interpolates:

   ```
   from: center + sweep * sweepDist + approach * hoverHeight
   to:   center - sweep * sweepDist - approach * hoverHeight * 0.5f
   ```

   A straight lerp between those two already reads as a diagonal chop through the plane.

4. **Impact.** The frame `t` crosses `ImpactT` the blade is at the plane. Fire `onImpact`
   exactly once — this is where `HandleCompletion` runs and the mesh actually splits. A
   particle burst here covers the geometry pop.

5. **Follow through.** Finish the swing. Then kick the severed piece: add a `Rigidbody` and
   `AddForce(-up * kick, ForceMode.Impulse)`, or lerp it along `-up` if you'd rather keep
   physics out of it.

## Editor preview

Two separate things, both edit-mode only, both driven off `EditorApplication.update` in the
shape of `Editor/CutPreview.cs`. Read that file first — it is the working precedent for
every hazard here, and the finisher's version should differ only where it has to.

### 1. Framing the shot

The camera really is moved: only the real camera has the game's FOV and aspect, so a Scene
view gizmo cannot tell you whether the chop is framed. What must not happen is the framing
session leaving the camera somewhere else when it ends.

Reuse the cut's own claim/restore rather than writing a second one:

```csharp
// CuttingManager -- one new parameter, no new snapshot path
internal void ClaimCamera(bool driveOrbit = true)
{
    CaptureCameraState();
    PushParameters();
    if (moveCamera != null) moveCamera.enabled = false;
    if (cameraFollow != null) cameraFollow.enabled = driveOrbit;   // was: always true
    RefreshLiveTuning();
    ...
}
```

The finisher preview claims with `driveOrbit: false` — it owns the pose, and an enabled
`CameraFollow` would overwrite it every editor tick — and releases through the existing
`ReleaseCamera()`, which restores `initialCameraPos/Rot/FOV` because `CaptureCameraState`
ran on claim. That is the same code path the real cut uses to put the camera back, so the
two cannot disagree about what "restored" means.

`FinisherPreview` then only has to:

- snapshot nothing of its own beyond the tool's local pose (the camera is handled above);
- each tick, place the camera on `TryGetCameraPose` and set `cam.fieldOfView = cameraFOV`,
  re-read every tick so editing the field moves the shot live rather than on restart;
- `SceneView.RepaintAll()` + `EditorApplication.QueuePlayerLoopUpdate()` — the Game view
  does not redraw on its own in edit mode, and it is the only view with the real framing;
- stop on `AssemblyReloadEvents.beforeAssemblyReload`, `playModeStateChanged` and
  `sceneClosed`. A recompile drops the statics; stopping first is what keeps the camera from
  being stranded with no way back.

Authoring ergonomics, in the `CutFinisher` inspector:

- **Frame shot** starts/stops the preview.
- **Grab from Scene view** writes the current `SceneView.lastActiveSceneView.camera` pose
  into `cameraPose` (`Undo.RecordObject` first). Compose in the Scene view with normal
  navigation, click once, done — much faster than nudging a child transform by hand.
- **Create pose** adds the `cameraPose` child under the body part if it is missing.

### 2. Previewing the cut

The slash preview must **never splice**. `SpliceWindowed` spawns GameObjects and swaps the
body's `sharedMesh`; running that in edit mode edits the scene for real, and there is no
undo path back to an uncut body. Use the non-destructive preview that already exists:

- `CuttingManager.SeveredPreviewMesh` — the piece this cut would take off, as a mesh in the
  body's local space, produced by running the real slicer and keeping the result without
  assigning it. Cached against the plane/body/window poses, so re-reading it per tick is a
  matrix compare, not a re-slice.
- `CutRegionHighlighter.For(body).Show(mesh, colour)` — draws that mesh as an overlay on the
  body. `Hide()` on stop.

So the preview timeline is:

| beat | what the preview shows |
|---|---|
| `EaseIn` | camera easing from where the cut left it to the shot, through `easeInCurve`; tool waiting, no highlight |
| `Wait` | camera on the shot, tool bobbing, no highlight |
| `Slash`, `t < ImpactT` | tool descending, severed piece highlighted green so you can see what it is about to take |
| `Slash`, `t ≥ ImpactT` | highlight turns red and is offset along `-approach * kick * (t - ImpactT)` to fake the piece coming away |
| `Hold` | the aftermath, held |

The offset is applied to the highlighter's transform, not the mesh, so nothing is
regenerated per frame and stopping restores it by identity.

**The preview runs the real beat, in real seconds.** It walks `EaseIn` → wait → `SlashTime` →
`HoldAfter` off the finisher's own numbers, re-read every tick, so editing any of them
reshapes the timeline immediately. An earlier version walked a synthetic 0..1 at a fixed
rate; that meant `SlashTime` and `AutoSlashAfter` had no visible effect at all, which is
worse than having no preview — you cannot tune a number the preview refuses to show you.

Two things the preview cannot take literally:

- **`AutoSlashAfter = 0` means "wait for the click forever"**, which a preview cannot do.
  It substitutes `PreviewWaitSeconds` (1.5s) and marks the figure with a `*` in the
  breakdown, so the wait you are watching is never mistaken for one you authored.
- **`TimeScale`** (0.05–2, default 1) is a playback rate, not a different timeline. An 0.18s
  slash is about eleven frames; judging it needs slow motion.

Controls: **Play / Pause / Stop**, a **seconds slider** that scrubs and pauses, **Go to
impact**, a **loop** toggle, and a breakdown line reading
`ease 0.50 + wait 1.50* + slash 0.18 + hold 0.25 (impact at 2.09s)` — so a beat that reads
wrong points straight at the field responsible. `SlashTime` of 0 gets its own warning, since
an instant swing looks like a broken one.

`Elapsed` doubles as the bob clock, so the bob animates through the wait and scrubbing shows
it exactly where that instant puts it.

### Mutual exclusion

`CutPreview` and `FinisherPreview` both take the scene camera. Two of them running means two
writers on one transform and neither snapshot is meaningful. Each `Start` must stop the
other — cheapest as one shared `EditorCameraClaim` static holding "who has the camera", with
both previews going through it, so a third preview later cannot reintroduce the bug.

## Gotchas

**Use `wasPressedThisFrame`, not `isPressed`.** `MoveCamera.CheckStartMinigame` enters the
minigame on a *held* left button. A player who never released would trigger the slash the
instant the hover loop starts.

**The splice returns the lower hulls only.** `CuttableObject.SpliceWindowed(plane)` hands back
the severed chunks as new `Lower_Hull*` GameObjects; the upper hull stays on the cuttable
itself. It returns an **empty list**, not null, on its early-out paths. `SliceOffPart` already
collapses that to one piece — errors on 0 and on >1, uses the first — and `HandleCompletion`
gets a possibly-null `GameObject` from it. Don't reintroduce the assumption when wiring the
kick: check for null before adding a `Rigidbody`.

**Camera ownership.** During `Finishing`, `cameraFollow` is off and `moveCamera` is still off
from `SetupRig`. Free-look is only handed back in `CompleteExit`, after the fly-out lands.
Nothing else may write `sceneCamera.transform` while the finisher is running — and in edit
mode that includes `CutPreview`, hence the mutual exclusion above.

**Splice timing vs. the kick.** The piece doesn't exist until `onImpact` returns, so the kick
has to happen after the callback, not alongside it.

**The severed piece's collider has to be made convex before it gets a Rigidbody.**
`SpawnPiece` gives every piece a plain `MeshCollider` — concave, which is right for the body,
since it never moves. PhysX cannot simulate a concave mesh collider on a *dynamic* body:
adding the Rigidbody logs *"Concave Mesh Colliders are not supported when used with dynamic
Rigidbody GameObjects"* and the piece falls through the world. `MakeCollidersDynamic` flips
`convex = true` first.

Convex rather than kinematic (the other half of what the error message suggests): a kinematic
body would swallow the impulse, which is the entire point of the kick. A severed chunk is a
loose prop, so its convex hull is a perfectly good collider — and only the piece is touched,
never the body, which keeps its exact concave shape for the next cut's raycasts.

**The camera is never handed a pose it did not fly to.** Three handovers, each easing from
wherever the previous one left the camera rather than from a stored ideal:

```
free-look ──SetupRig, enterTravelTime──▶ orbit
orbit ──BeginFinisher (no move) → CutFinisher easeIn──▶ the shot
the shot ──RestoreRig → BeginTravel(Exiting), exitTravelTime──▶ free-look
```

`RestoreRig` deliberately does **not** call `ReleaseCamera` — that assigns the free-look pose
outright, which is the very thing the fly-out is animating. `CompleteExit` calls it on
landing, where `ApplyTravel` has already put the camera exactly on target, so there is no
discontinuity. This was a real bug once; the comment at `RestoreRig` is there to stop it
coming back.

**The tool outlives the coroutine.** `Run` does not `ReleaseTool` before `onDone`: `onDone`
starts the fly-out, and the tool is still in shot for all of `exitTravelTime`. Destroying it
at the end of the swing pops it out of a frame the player is watching.
`CuttingManager.CompleteExit` releases it when the camera lands.

**The fly-out borrows the cut's `exitTravelTime`.** 0.45s is tuned for the orbit-to-free-look
distance; a tight close-up is a much bigger move over the same 0.45s. If the exit ever reads
as rushed, that is the field, not the finisher. `exitTravelTime` of 0 snaps outright, since
`Advance` returns 1 on the first frame.

**The plane may have moved.** `LoopGuideBuilder` re-extracts its loop whenever the plane or
mesh transform changes. If anything animates the `CutPlane` during the finisher, the cut
lands somewhere other than where the guide showed. Keep it still. Same reason
`SeveredPreviewMesh` keys its cache on the plane and body poses — a moved plane silently
invalidates a preview that still looks correct.

**The preview must not dirty geometry.** `SeveredPreviewMesh` builds throwaway meshes and
frees them; the highlighter's overlay child is `HideAndDontSave`. Any new preview code that
assigns a mesh, spawns a piece, or calls the splicer has escaped that and will edit the scene
for real. The camera pose is restored on stop; a spliced body is not.

## Answered

- **Tool prefab: wired per cut**, on `CutFinisher.toolPrefab`. Not resolved from
  `requiredToolName`, which stays what it always was — a gate on entry.
- **`enableFinisher`** opts the whole beat out, per cut.
- **A timeout exists**: `autoSlashAfter`, 0 = wait forever, defaulting to 0.
- **The tear sound moved to the impact frame** as a side effect of the `ApplySplice` /
  `FinishUp` split: it fires with the splice, which is now under the blade rather than at
  the end of the run.

## Open questions

- Does `cameraPose` want to be a Transform at all, or a `CutFinisherPreset` ScriptableObject
  like every other tuning block in this subsystem? The pose is per-shot and cannot live in an
  asset (scene reference), but everything else in the field list can, and the split would match
  `CutMinigamePreset`.
