# Cut Setup Checklist

What every cut needs before it runs. The **New Cut Minigame** tool builds and wires most of it; this
list is the part a person still has to author per body part. The cut's own inspector shows a live
**"Not runnable yet, missing:"** box — anything below that you skip shows up there, because
`CuttingManager.MissingWiring()` checks all of it.

> Make a cut: `GameObject > Cutting > New Cut Minigame` (right-click the body, or from the menu bar
> with the body selected). It creates the `Cut (...)` object under the body, wires the manager, loop
> guide, guide lines, a `plane` cube, a `Scalpel`, the free-look and the camera orbit, and assigns the
> shared audio presets if the project has them. Then do the steps below on that object.

---

## Per-cut checklist

### 1. Object being cut
The body this cut is on. Auto-filled when the cut is created under a `CuttableObject`; only set it by
hand if you built the cut somewhere else. → **`GameObjectBeingCut`** on the manager.

### 2. Place the plane
The `plane` cube **is** where the cut goes — the tool drops it at the body's centre, which is a
starting point, not an answer.
- Move/rotate the cube so its flat face sits where the ring should open.
- Size the cut **window** by dragging the cube's `BoxCollider` handles (X and Z only; Y is ignored).
  What the box outlines is what gets cut. Leave the collider **disabled** — it is an authoring handle;
  enabled, it swallows the aim raycast and the cut can't be entered.
- Turn on the body's **Draw Cut Loops** to see the green loop update live as you drag.

### 3. Start / End angle
Where the cut opens and closes around the ring, in degrees. → **`startAngle`**, **`endAngle`** on the
manager.
- `startAngle` = where progress is measured from. `endAngle = 360` is one full turn.
- **They must not be equal.** Equal angles are a zero-length cut — the ring never opens. The validator
  flags this.
- These are per-cut geometry and are never taken from a preset.

### 4. Item name
What the piece this cut takes off is called, e.g. `"Left Leg"`. → **`itemName`** on the manager.
- Becomes the severed object's name and its `GrabbableObject.itemName`, so the rest of the game asks
  for it by name. An empty name is flagged.

### 5. Body part
The **`BodyPart`** ScriptableObject asset the detached piece is built from (identity, prefab, size).
→ **`bodyPartType`** on the manager.
- Pick an existing asset under `Assets/Art/BodyParts/`, or make one:
  `Create > Scriptable Objects > BodyPart`. A missing asset is flagged.

### 6. Choose a scalpel (cutting-phase tool)
The `Scalpel` object the tool created drives the trace during the cut. Give it a **mesh** so it is
something the player sees — `EnsureScalpelDriver` leaves that to you.
- Add a MeshFilter/MeshRenderer (or drop a saw/scalpel model under it) and place it.
- Its `ScalpelSurfaceDriver` + `CameraFollow` are already wired to the manager (`scalpelFollow`). If
  you replaced the object, re-run **Auto-wire**. A missing scalpel `CameraFollow` or its
  `ScalpelSurfaceDriver` is flagged.

### 7. Set up the finisher (mandatory)
The finisher is the close-up one-click chop that ends the cut. **Every cut must have one, enabled,
framed, and holding a tool** — the validator flags each missing piece.
- **Have a finisher:** a `CutFinisher` on the cut, with **Enable Finisher** on. (The tool builds one
  with the cut.) A missing or disabled finisher is flagged.
- **Frame the shot:** compose the Scene view camera on the cut, then on the `CutFinisher` inspector
  press **Grab from Scene view**. Fine-tune with the Scene handles or `shotLocalPosition` /
  `shotLocalEuler`. No shot is flagged.
- **Choose the finisher tool:** assign **`toolPrefab`** — the saw/cleaver that swings. It can be a
  **plain scene object** (swung where it sits) or a **prefab asset** (a copy is spawned). No tool is
  flagged.
- Preview without pressing play: use the finisher's edit-mode **preview** controls to judge framing and
  the swing.

---

## What the validator checks

`CuttingManager.MissingWiring()` (shown in the manager inspector, and used by the setup menu and the
Cut Copy window) reports a cut as **not runnable** when any of these is missing:

| Message | Fix (step) |
|---|---|
| Object being cut | 1 |
| Loop guide / A CutPlane for the loop guide | created by the tool; place it → 2 |
| Scene camera / A CameraFollow on the scene camera | scene needs a camera; Auto-wire |
| Free-look MoveCamera | created by the tool; Auto-wire |
| Scalpel CameraFollow / A ScalpelSurfaceDriver on the scalpel | 6 |
| Camera moves preset | assign it in the manager's "How it plays" section |
| **Item name (what the severed piece is called)** | 4 |
| **Body part (a BodyPart asset)** | 5 |
| **Start/End angle span (they are equal, so the cut has no length)** | 3 |
| **Finisher (a CutFinisher on the cut)** | 7 |
| **Finisher enabled (its Enable Finisher is off)** | 7 |
| **Finisher camera shot (frame it, then Set Shot From Camera)** | 7 |
| **Finisher tool (a scalpel/saw for the finisher to swing)** | 7 |

These are the per-cut authoring checks. The finisher is mandatory: the shot and tool rows appear once
the finisher is present and enabled; before that you get the missing/disabled row instead.

> Note: the validator only checks that these are *filled in*. It cannot tell you the plane is in a
> sensible spot or the shot is framed well — that is what Draw Cut Loops and the finisher preview are
> for.
