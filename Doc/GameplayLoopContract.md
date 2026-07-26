# Gameplay Loop Contract

Last updated: 2026-07-24

This is the persistent source of truth for the main gameplay loop. Read it
before implementing or changing gameplay systems.

Plain-language teammate setup and debug instructions are also available at:

`Assets/Data/GameplayManagerTeamGuide.txt`

## Core ownership rule

`GameplayManager` coordinates the day. It should call public functions and
listen to events owned by other systems. It should not implement surgery,
doctor AI, inventory, cutting, storage, or black-market mechanics itself.

Every integration must identify:

- The system owner.
- The public function called by the coordinator.
- The event raised when work completes or fails.
- The data passed through that function or event.
- Whether the integration is implemented, temporary, or only planned.

## Beginning of Day

Required sequence:

1. If `Require Asset Validation` is enabled, validate all required scene
   references: exactly two distinct beds, one trapdoor/chute, one storage
   object, and at least one cutting tool.
2. When validation is enabled, stop immediately and log an error if it fails.
3. Enter `Preparing`.
4. Generate the client/task queue without spawning clients.
5. Generate the black-market body-part order.
6. Publish both lists to their UI displays.
7. Enter `InProgress`.
8. Raise `DayStarted`.
9. Each empty operation chair spawns one pending client.
10. Start the day countdown.
11. Enable doctor behavior and player interaction.

Current APIs:

```csharp
gameplayManager.BeginDay();
clientList.GenerateList();
blackMarketGenerator.GenerateTask(dayNumber);
gameplayManager.DayStarted += HandleDayStarted;
```

Current implementation:

- Optional asset validation before day startup: implemented.
- Client/task queue generation: implemented.
- Black-market task generation: implemented.
- `GameplayManager` phase/state changes: implemented.
- Operation chairs filling on `DayStarted`: implemented.
- Client-list world-space UI: planned.
- Black-market UI: planned.
- Day countdown start: planned.
- Doctor activation: planned.

Important configuration:

- `GameplayAssetChecker` is optional when `Require Asset Validation` is off.
- When `Require Asset Validation` is on, the checker must contain exactly two
  distinct `OperationChair` references. Missing or incorrectly wired beds
  prevent the day from starting.
- `GameplayAssetChecker` also requires trapdoor, storage, and at least one
  cutting-tool reference. A missing client-list poster only produces a warning.
- `GameplayManager` owns beginning-of-day generation and requires a
  `BlackMarketGenerator` reference.
- Exactly one scene-scoped `RandomizedClientList` owns the shared queue.
  GameplayManager and both chairs access it through
  `RandomizedClientList.Instance`.
- Disable `Prepare On Start` on `RandomizedClientList`.
- Pre-generated clients are data only; no client GameObject exists until an
  operation chair calls `SpawnNextClient`.
- `RandomizedClientList` obtains each queue entry's prefab from
  `CustomersAsset.GetRandomCustomerAsset()` and stores the returned prefab on
  that entry before spawning.
- When a spawned customer prefab has no `ClientTaskHolder`,
  `RandomizedClientList` adds the component to the runtime instance before
  assigning its task.

## During the Day

Expected loop:

1. Two operation chairs manage their occupants independently.
2. The doctor requests one valid item or body part at a time.
3. The player performs required surgery for clients.
4. The player may secretly cut additional parts for the black market.
5. Cutting creates physical items that fall into the world.
6. The doctor periodically moves, operates, and checks the player.
7. The player loses a heart if caught doing forbidden cutting.
8. The player loses a heart or suffers the agreed penalty when the countdown
   expires.
9. Completing a client task removes that client and its task-list entry.
10. The now-empty chair automatically spawns the next pending client.
11. When no pending clients remain, that chair stays empty.

Current APIs:

```csharp
GameObject client = clientList.SpawnNextClient(operationChair);
bool accepted = clientTaskHolder.GiveBodyPart(bodyPart);
bool updated = clientList.RemoveOneFromTask(targetClient, bodyPart);
bool removed = clientList.DespawnPerson(client);
bool spawned = operationChair.TrySpawnNextClient();
toolRequestManager.BuildRequestsForClient(operationChair, client);
bool doctorAccepted =
    toolRequestManager.PlayerSubmittedTool(itemName, itemType);
```

Current events:

```csharp
clientTaskHolder.TaskAssigned
clientTaskHolder.TaskCompleted
clientTaskHolder.TaskCompletedWithOwner
clientList.ClientSpawned
clientList.ClientSpawnedOnChair
clientList.ClientRemoved
clientList.TaskListEmptied
clientList.TaskRequirementChanged
operationChair.ClientPlaced
operationChair.ClientLeft
clientDialogueEventChannel.DialogueRequested
toolRequestManager.RequestStarted
toolRequestManager.RequestCompleted
toolRequestManager.RequestFailed
toolRequestManager.EarlyCompletionBonusAwarded
toolRequestManager.RequestQueueEmptied
cameraSwitch.ViewStateChanged
```

Current implementation:

- Independent tasks and progress per client: implemented.
- Spawned queue entries record their assigned `OperationChair`, allowing
  systems to distinguish Bed A from Bed B: implemented.
- Automatic removal on client-task completion: implemented.
- Automatic chair refill: implemented.
- Operation chairs spawn clients using a character pose proxy's world
  position, rotation, and scale: implemented.
- Client task dialogue requests through a decoupled event channel: implemented.
- Queued client dialogue UI receiver: implemented.
- Empty client list triggers end-of-day validation: implemented.
- A doctor-request batch completes its focused client task when the batch
  reaches zero: implemented.
- Inspector gameplay-loop debug harness for accepted-order and fast-forward
  testing: implemented.
- `RandomizedClientList` provides an editor context command that completes
  every generated task through the normal delivery API and consequently
  raises `TaskListEmptied`: implemented.
- Occupied chairs provide an editor-only context command that completes their
  current client's remaining requirements through the normal task API:
  implemented.
- `ToolRequestManager` provides an editor-only context command that
  force-completes its active request and starts the normal cooldown:
  implemented.
- The doctor processes one client batch at a time in configured chair order:
  Bed A, Bed B, then back to Bed A: implemented.
- `WalkState` reads `ToolRequestManager.FocusedChair`, selects that bed's
  navigation waypoints, and faces its current client after arriving:
  implemented.
- Only the focused client's requirements populate the doctor queue. All
  requests in that batch retain the focused client and chair: implemented.
- When the focused batch reaches zero, the manager completes that client's
  remaining `ClientTask`, allowing the normal removal and chair-refill events
  to run before focus advances: implemented.
- Failed requests return to the current batch and must succeed before the
  focused client completes: implemented.
- Successful doctor requests add their unused request time to the normal
  cooldown. The final request delays client completion by that rewarded
  cooldown, creating a storage/chute preparation window: implemented.
- `CameraSwitch` owns `MainGame` and `BlackMarket` view states. Entering the
  black-market camera pauses scaled gameplay; returning restores the previous
  time scale. Doctor request and cooldown counters therefore retain their
  remaining values: implemented.
- Surgery/cutting success integration: planned.
- Secret versus required cutting classification: planned.
- Doctor detection and heart penalty: planned.
- Countdown and timeout penalty: planned.
- Chute registration and per-chute debug entry counting: implemented.
- Fridge storage exposes live count, capacity, and per-slot body-part
  type/health information: implemented.
- `BodyPartRunSummary` persists per-type chute and fridge counts across the
  gameplay-to-Win/Lost scene transition. Result UI displays combined Hand,
  Leg, Nose, and Ear totals: implemented.
- End-of-day freezer/storage/decay integration: planned.

## End of Day

Required sequence:

1. Stop the countdown.
2. Disable new client spawning and doctor task generation.
3. Resolve or remove remaining clients according to the final design.
4. Count body parts in the room.
5. Count body parts submitted to black-market storage.
6. Count parts kept in the freezer.
7. Compare delivered black-market parts with the day order.
8. Award completed-order value and handle extra parts.
9. Process decay for unprotected body parts.
10. Preserve freezer contents, with a maximum capacity of three.
11. Calculate client tasks completed and other day statistics.
12. Show the day-results UI.
13. Enter `Ended`.
14. Advance only after results are acknowledged.

Current APIs:

```csharp
gameplayManager.EndDay();
gameplayManager.AdvanceToNextDay();
gameplayManager.DayEnded += HandleDayEnded;
gameplayManager.BlackMarketTaskResolved +=
    HandleBlackMarketTaskResolved;
gameplayManager.EnterBlackMarketRequested.AddListener(
    HandleEnterBlackMarket);
```

Current implementation:

- `Ended` state and `DayEnded` event: implemented.
- Inspector-configurable enter-black-market request invoked by `EndDay`:
  implemented.
- `EndDay` resolves the generated black-market order first and publishes its
  success/failure through `BlackMarketTaskResolved`: implemented.
- `TestFinishDay` listens to `BlackMarketTaskResolved` and loads the Win or
  Lost result scene on the following frame: implemented.
- Advancing the day number: implemented.
- Automatic ending when the client list reaches zero, the player has health
  remaining, and the temporary countdown remaining is nonnegative:
  implemented.
- Body-part counting: planned.
- Black-market requirement-slot completion check: implemented.
- Black-market scoring: planned.
- Extra-part scoring: planned.
- Room/storage/freezer separation: planned.
- Decay processing: planned.
- Freezer capacity enforcement: planned.
- Results UI: planned.

Temporary integration:

- `GameplayManager` reads health through `HealthScript.Instance`. Its
  serialized temporary lives value is only a fallback for scenes without a
  `HealthScript`.
- `CountdownRemaining` remains a placeholder until the final countdown system
  exposes its API.
- If the client list reaches zero without the expected lives/countdown state,
  the manager logs a warning because that path should not normally be reachable.

## Black-market integration boundary

`BlackMarketGenerator` is the current implementation used by the
beginning-of-day loop.

The generator implements:

```csharp
public interface IBlackMarketTaskGenerator
{
    BlackMarketTask GenerateTask(int dayNumber);
}
```

Assign the `BlackMarketGenerator` component to `GameplayManager`.

## Change checklist

Whenever gameplay code changes:

1. Identify which phase owns the behavior.
2. Preserve the phase ordering above unless the team agrees to change it.
3. Prefer events across system boundaries.
4. Do not mark planned behavior as implemented without verifying it.
5. Update this document with new functions, events, and ownership.
