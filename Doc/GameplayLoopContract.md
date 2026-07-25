# Gameplay Loop Contract

Last updated: 2026-07-24

This is the persistent source of truth for the main gameplay loop. Read it
before implementing or changing gameplay systems.

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

1. Validate all required scene references, including exactly two distinct beds.
2. Stop immediately and log an error if validation fails.
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

- Required two-bed validation before day startup: implemented.
- Client/task queue generation: implemented.
- Temporary black-market task generation: implemented.
- `GameplayManager` phase/state changes: implemented.
- Operation chairs filling on `DayStarted`: implemented.
- Client-list world-space UI: planned.
- Black-market UI: planned.
- Day countdown start: planned.
- Doctor activation: planned.

Important configuration:

- `GameplayAssetChecker` must contain exactly two distinct `OperationChair`
  references. Missing or incorrectly wired beds prevent the day from starting.
- `GameplayManager` owns beginning-of-day generation.
- Disable `Prepare On Start` on `RandomizedClientList`.
- Pre-generated clients are data only; no client GameObject exists until an
  operation chair calls `SpawnNextClient`.

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
GameObject client = clientList.SpawnNextClient(chairTransform);
bool accepted = clientTaskHolder.GiveBodyPart(bodyPart);
bool removed = clientList.DespawnPerson(client);
bool spawned = operationChair.TrySpawnNextClient();
```

Current events:

```csharp
clientTaskHolder.TaskAssigned
clientTaskHolder.TaskCompleted
clientTaskHolder.TaskCompletedWithOwner
clientList.ClientSpawned
clientList.ClientRemoved
operationChair.ClientPlaced
operationChair.ClientLeft
```

Current implementation:

- Independent tasks and progress per client: implemented.
- Automatic removal on client-task completion: implemented.
- Automatic chair refill: implemented.
- Doctor item requests: exists separately; integration not confirmed.
- Surgery/cutting success integration: planned.
- Secret versus required cutting classification: planned.
- Doctor detection and heart penalty: planned.
- Countdown and timeout penalty: planned.
- Physical body-part item registration: planned.
- Freezer/storage/decay integration: planned.

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
```

Current implementation:

- `Ended` state and `DayEnded` event: implemented.
- Advancing the day number: implemented.
- Body-part counting: planned.
- Black-market order resolution and scoring: planned.
- Extra-part scoring: planned.
- Room/storage/freezer separation: planned.
- Decay processing: planned.
- Freezer capacity enforcement: planned.
- Results UI: planned.

## Black-market integration boundary

The temporary generator exists only so the beginning-of-day loop can run
before the final black-market system is ready.

The final implementation must implement:

```csharp
public interface IBlackMarketTaskGenerator
{
    BlackMarketTask GenerateTask(int dayNumber);
}
```

Then assign that implementation to `GameplayManager`. The day coordinator
should not require changes.

## Change checklist

Whenever gameplay code changes:

1. Identify which phase owns the behavior.
2. Preserve the phase ordering above unless the team agrees to change it.
3. Prefer events across system boundaries.
4. Do not mark planned behavior as implemented without verifying it.
5. Update this document with new functions, events, and ownership.
