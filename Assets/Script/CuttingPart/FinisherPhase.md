# Finisher phase — design note

Status: **not implemented.** This is the plan for the beat between "the player closed
the loop" and "the mesh is actually sliced".

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

`BeginFinisher` parks the cutting rig without restoring the camera, then hands off:

```csharp
void BeginFinisher()
{
    phase = RigPhase.Finishing;

    cameraFollow.enabled = false;   // stop the orbit; the finisher poses the camera
    speedDriver.SetSignedSpeed(0);
    speedDriver.Disable();
    SetScalpelTrace(false);

    finisher.Begin(GameObjectBeingCut, HandleCompletion);
}
```

`HandleCompletion` keeps its body exactly as it is — splice, `InstantiateBodyPart`,
`QuitMinigame`, fire `OnMinigameCompleted`. It is now invoked by the finisher on the impact
frame rather than by `Update`.

Note `QuitMinigame` guards on `isPlaying` (`phase == Cutting`), so it will need to accept
`Finishing` too, or `BeginFinisher` should defer the phase flip until the callback fires.

## CutFinisher component

New MonoBehaviour, owns all of the presentation. `CuttingManager` knows only `Begin` and the
callback.

```csharp
public Transform cameraPose;      // empty in the scene, child of the body part
public Transform tool;            // tool instance, hidden until Begin
public float easeIn = 0.5f;       // seconds to reach cameraPose
public float hoverHeight = 0.25f; // along the plane normal
public float bobAmp = 0.02f, bobHz = 1.5f;
public float sweepDist = 0.6f;    // half-travel across the limb
public float slashTime = 0.18f;
public AnimationCurve slashEase;
public float kick = 3f;           // impulse on the severed piece
```

Author `cameraPose` as a Transform placed by hand in the scene rather than computing an
offset. Framing a chop is a shot composition problem; you want to eyeball it per body part.

### Frame basis

Everything derives from the cuttable's `planeTransform`:

- `planeTransform.up` — the plane normal. The blade travels **down** this to bite.
- `planeTransform.right` — the sweep axis. The blade travels **across** this.
- `planeTransform.position` — the plane point, close enough to the cut centre for framing.

`LoopGuideBuilder` exposes the same basis as `PlaneNormal` / `PlaneRight` / `PlaneForward`,
plus `LoopCenter` for the true centre of the ring if the plane origin is off to one side.

### The coroutine

1. **Ease in.** Lerp `sceneCamera` position/rotation to `cameraPose` over `easeIn`.
   Nothing needs saving — `CuttingManager.initialCameraPos/Rot/FOV` already hold the
   free-look snapshot that `CompleteExit` restores.

2. **Hover.** Park the tool at `center + up * hoverHeight`, bobbing on
   `Mathf.Sin(Time.time * bobHz * TAU) * bobAmp` along `up`. Orient it so the blade edge is
   perpendicular to `up` and parallel to `right`. Loop until
   `Mouse.current.leftButton.wasPressedThisFrame`.

3. **Slash.** Lerp `t` 0→1 over `slashTime` through `slashEase`:

   ```
   from: center + right * sweepDist + up * hoverHeight
   to:   center - right * sweepDist - up * hoverHeight * 0.5f
   ```

   A straight lerp between those two already reads as a diagonal chop through the plane.

4. **Impact.** The frame `t` crosses 0.5 the blade is at the plane. Fire `onImpact` exactly
   once — this is where `HandleCompletion` runs and the mesh actually splits. A particle
   burst here covers the geometry pop.

5. **Follow through.** Finish the swing. Then kick the severed piece: add a `Rigidbody` and
   `AddForce(-up * kick, ForceMode.Impulse)`, or lerp it along `-up` if you'd rather keep
   physics out of it.

## Gotchas

**Use `wasPressedThisFrame`, not `isPressed`.** `MoveCamera.CheckStartMinigame` enters the
minigame on a *held* left button. A player who never released would trigger the slash the
instant the hover loop starts.

**The splice returns the lower hull only.** `CuttableObject.SpliceWindowed()` hands back the
severed chunks as new `Lower_Hull*` GameObjects; the upper hull stays on the cuttable itself.
It returns `null`, not an empty list, on every failure path. `HandleCompletion` already
guards this — don't reintroduce the assumption when wiring the kick.

**Camera ownership.** During `Finishing`, `cameraFollow` is off and `moveCamera` is still off
from `SetupRig`. Free-look is only handed back in `CompleteExit`, after the fly-out lands.
Nothing else may write `sceneCamera.transform` while the finisher is running.

**Splice timing vs. the kick.** The piece doesn't exist until `onImpact` returns, so the kick
has to happen after the callback, not alongside it.

**The plane may have moved.** `LoopGuideBuilder` re-extracts its loop whenever the plane or
mesh transform changes. If anything animates `planeTransform` during the finisher, the cut
lands somewhere other than where the guide showed. Keep it still.

## Open questions

- Does the tool prefab come from the `toolNeeded` string, or is it wired per `CuttingManager`?
  The string is currently only used as a gate in `canEnterMinigame`.
- Should a slash be skippable / auto-fire after a timeout, so a player who never clicks isn't
  stuck? Nothing in `Finishing` currently times out.
- Sound and haptics land on the impact frame; not specced here.
