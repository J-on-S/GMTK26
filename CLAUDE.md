# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 6.3 LTS (`6000.3.19f1`), URP 17.3, game jam project (GMTK 2026). Surgery/black-market game: the player cuts body parts off clients for doctor requests while secretly filling black-market orders.

All code is in the default assembly (`Assembly-CSharp`) — there are no runtime asmdefs, so every script sees every other script and most classes live in the global namespace (exceptions: `EzySlice`, `BuildTools`, `SceneTools`).

## Working in this repo

There is no CLI build/test workflow — this is an Editor-driven project.

- **Build**: press Unity's Build button (intercepted by `BuildConfigurator`, which opens a modal config window; cancelling aborts the build), or `Tools > Build > Dated Build (active target)` for a zipped, date-stamped folder build that never uploads.
- **Build config**: `BuildConfig` ScriptableObject (`Build Tools/Build Config`). Scripting defines set there apply to that build only — `PlayerSettings` is never mutated. itch.io upload goes through butler and is configured per-machine in `EditorPrefs`.
- **Scene bootstrap**: `SceneBootstrapInjector` auto-adds prefabs listed in a `SceneBootstrapConfig` to every scene opened or created. If a scene gains unexpected objects on open, that's why.
- **Tests**: `com.unity.test-framework` is installed but there are no test assemblies. Files named `*Test*` (`GameplayLoopDebugTester`, `ClientTaskDebugTester`, `TestConversationSystem`, `bedTest`) are in-Editor inspector debug harnesses, not automated tests. Verification is done by entering Play mode.
- **Scenes**: build settings only enable `StartScreen`, `SampleScene`, `PauseMenu`. Many `Assets/Scenes/*.unity` files are per-feature/per-person work scenes (`Cutting.unity`, `Game.unity`, `DoctorRequest.unity`, …).
- **Git**: `.gitattributes` routes Unity YAML through `unityyamlmerge`; `.unity`/`.prefab`/`.asset` conflicts should be resolved with Unity's smart merge, not by hand.

## Mandatory reading before gameplay changes

`AGENTS.md` requires reading `Doc/GameplayLoopContract.md` before touching gameplay flow, managers, clients, operation chairs, doctor behavior, countdowns, black-market orders, storage, or end-of-day logic. That contract is the source of truth for the day loop and explicitly marks each behavior as **implemented / temporary / planned** — do not assume a described system exists. Update the contract when an API, event, owner, or phase responsibility changes.

Plain-language teammate setup notes live in `Assets/Data/*.txt` (`GameplayManagerTeamGuide.txt`, `GameplayManagerSetup.txt`, `ClientTaskSetup.txt`, `ClientDialogueSetup.txt`, `GameplayLoopDebugSetup.txt`).

## Architecture

### Day coordination (`Assets/Script/Gameplay/`)

`GameplayManager` owns only phase flow (`NotStarted → Preparing → InProgress → Ended`) and raises `DayStarted` / `BlackMarketTaskGenerated` / `DayEnded`. **It must not implement surgery, doctor AI, inventory, cutting, storage, or black-market mechanics** — it calls other systems' public functions and listens to their events.

- `BeginDay()` hard-fails unless a sibling `GameplayAssetChecker` validates the scene (exactly two distinct `OperationChair`s, trapdoor, storage, ≥1 cutting tool).
- Black-market generation goes through `IBlackMarketTaskGenerator`; `TemporaryBlackMarketTaskGenerator` is a placeholder wired via a `MonoBehaviour` field and cast at runtime.
- `NumberOfLives` / `CountdownRemaining` are placeholders until the real lives and countdown systems exist.

### Clients and tasks (`Assets/Script/General/ClientScript/`, `Gameplay/OperationChair.cs`)

`RandomizedClientList` pre-generates client+task entries as **data only** — no GameObject exists until an `OperationChair` calls `SpawnNextClient`. Chairs refill themselves when their occupant's task completes. Completion removes the client from both the active list and the task list; an emptied task list triggers end-of-day.

### Cutting minigame (`Assets/Script/CuttingPart/`) — the largest subsystem

One `CuttingManager` (~1.4k lines) = one cut of one body part. It sits on a **child** of the body, so a body can carry several cuts; `CutRegistry` maps `CuttableObject → cuts` with a lazily rebuilt cache invalidated on manager enable/disable.

Two orthogonal state axes on the manager: `CuttingState` (how far the cut got) and `RigPhase` (`Free → Entering → Cutting → Finishing → Exiting`, where the camera is). During `Entering`/`Exiting` the manager flies the camera itself; `CameraFollow` and `MoveCamera` are off.

Collaborators: `LoopGuideBuilder` (target loop geometry), `CutPlane` (where the cut goes, plus its bounds window), `CameraFollow` (orbit), `CutSpeedDriver`/`ScalpelSurfaceDriver` (`ISpeedSource` input), `LoopScorer`, `CutTracer`, `CutFinisher` (close-up + one-click chop that fires the actual splice at `ImpactT`).

Mesh surgery is `CuttableObject.SpliceWindowed(CutPlane)` on top of the vendored **EzySlice** library (`Assets/Script/EzySlice/`, modified). `CuttableObject` holds only mesh-level facts (weld distance, cross-section material) and takes the plane as an argument — it never stores "which cut is happening".

Two conventions worth preserving:

- **Presets over inline fields**: `CutFinisherPreset`, `CameraFollowPreset`, `ScalpelSurfacePreset`, `CameraMovesPreset`, `CutSoundPreset` (menu `Cutting/…`). Each preset covers *one* collaborator's feel, and is assigned per cut on the `CuttingManager`. `CutFinisherPreset` still falls back to the component's inline field when unassigned; keep both in step when adding tuning there. There is deliberately no cut-wide preset — `CutMinigamePreset` was removed because one asset spanning framing, feel and geometry could not be retuned for a wrist without moving the thigh. Cut-level numbers (`cameraFOV`, `scalpelAngleLead`, guide line width, both orbit presets) live on the manager only.
- **One guide line, flat**: a cut draws the raw flat cross-section into `flatLine` and nothing else. The wavy "curved guide" is dormant code inside `LoopGuideBuilder` only — no `loopLine`, no `CurvePreset` assigned, and the camera, the scalpel driver and the finisher all read `TryGetFlatLoop`. Don't re-introduce curve fields on `CuttingManager` or the follow presets.
- **Editor authoring**: `GameObject > Cutting > New Cut Minigame` builds the wired hierarchy in code rather than from a prefab (a prefab would carry stale per-scene references to camera/scalpel/speed driver). `CuttingManager.AutoWire()` / `MissingWiring()` back that up; several components are `[ExecuteAlways]` so authoring previews in edit mode.

### Cross-system communication

ScriptableObject event channels decouple senders from receivers — `AudioEventChannel`, `CamShakeEventChannel`, `ClientDialogueEventChannel` (assets in `Assets/Data/`). Callers hold the channel asset and invoke; `AudioMaster` / `CamShake` / `ClientDialogueUIReceiver` subscribe. Prefer this pattern over `FindObjectOfType` for new cross-system links; the gameplay contract also requires events across system boundaries.

### Doctor AI (`Assets/Script/Doctor_AI/`)

Classic ScriptableObject-ish state machine: `StateManager` holds a `State`, calls `UpdateState()` each frame and switches when it returns a different state. States: `IdleState`, `WalkState` (NavMesh), `SurgeryState`, `CheckState`. `StateManager.RandomState(List<StateWeight>)` does weighted random selection.

### Interaction (`Assets/Script/Interaction/`)

`Interactor` on the player raycasts from viewport center each frame and calls `IInteractable.Interact(this)` on left-click; holds at most one `GrabbableObject`. Note: `Interactor` uses legacy `Input.GetKeyDown`/`GetMouseButtonDown` while the cutting minigame uses the new Input System (`UnityEngine.InputSystem`) — both are live in this project.

## Style notes

The cutting and build-tools code documents *invariants* and *why a design was chosen* in XML doc comments (e.g. why a reference is resolved instead of serialized, why a pose is snapshotted by value). Match that when editing those files. Older gameplay/AI code is much lighter — match the local file instead of imposing one style.
