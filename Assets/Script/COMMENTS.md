# Comment & doc style

Docstring authoring rules for this codebase. These govern `///` XML doc comments on C# members. Relocated here from `CLAUDE.md` to keep that file lean; an AI agent editing the code should follow these when writing or revising comments.

## Which of these also apply to README prose

The folder `README.md` files are prose, not XML docstrings, so the tag-specific rules (`<summary>`, `<param>`, `<returns>`, `<c>` tags, `<inheritdoc/>`) do not apply to them. The rules that **do** carry over to README writing:

- **No stale references** — no commit history, PR numbers, internal-only file names (`findings.md`, design plans that don't ship), or "used by X" cross-refs that only locate another member.
- **Purpose before mechanism** — lead with what a thing is for, then how it works.
- **Plain words** — everyday term over ornate one; no filler; split sentences over 30 words and 3+ em-dash chains.
- **Say each fact once** — don't restate the same fact in two places.
- **Trim padding** — cut trailing `so…` / `because…` clauses that re-explain what was already stated.

## Docstring rules

Use `///` XML doc comments for all members — methods, properties, fields, and events (public and private) — **except** when the summary could only echo the member name and signature, adding no fact a reader couldn't already write from them. This is the same echo test as the `<param>` rule below, applied one level up to the member itself:

- **Method** — `GetDebugLabel` → "Returns the debug label", `Reset` → "Resets the state": omit.
- **Property / field** — `Speed` → "The speed", `forwardSelector` → "The forward selector", an auto-property whose name fully names its value: omit.
- **Event** — `OnDestroyed` → "Fires when destroyed": omit.

Keep the docstring the moment it carries a non-obvious fact the name does not promise — a unit, a range or default, an edge case, the consumer, an invariant, a side effect, a guard, when an event fires beyond the literal name, or a surprising role. When in doubt whether a name is self-evident, write the one sentence you'd use: if it only re-spells the name, drop it; if it states something new, keep it.

- `<summary>` — exactly one sentence: *purpose* (what it's for), not mechanism (how it works). One sentence means one period — a second clause is never needed; move secondary context to `<remarks>` or drop it. No fragments. No vague verbs (`handles`, `sets up`, `processes`, `manages`). No restating the signature. No design rationale (`Code-only because…`, `Defined explicitly so…`, `Subscribes to X rather than polling`).
- `<param>` — one line when role isn't obvious: when it adds a fact the name, type, and class `<summary>` do not already supply (a unit, a default, a constraint, an edge case, a non-obvious or surprising role). Omit it when a reader could write the same line from the signature alone — that's not documentation, it's an echo. Role-named (`forward`, `side`), not math notation (`u`, `v`).
- `<returns>` — what the value means, including edge cases (`null`, `false`, `-1`, `Vector3.zero`).
- `<remarks>` — non-obvious invariants only. Prefix each `Invariant:`. State behavioral *consequence*, not the mechanism producing it.
- Wrap all code references (field names, type names, `null`, `true`, `false`, numeric thresholds) in `<c>` tags.

**Purpose, not mechanism.**
- ❌ `Computes orthonormal basis from chord vector via cross-product fallback.` — mechanism-first.
- ✅ `Builds the per-segment frame used by curve shapes to lay out points off the straight chord.`

**One sentence means one sentence.**
- ❌ `Completes when the watched agent's State matches targetState. Subscribes to OnStateChanged rather than polling. Code-only because the watched agent is scene-bound.` — three sentences; only the first is purpose.
- ✅ `Completes when the watched agent's State matches targetState.`

**`<param>` that only echoes the name.**
- ❌ `<param name="seconds">Duration to wait in seconds.</param>` on `WaitSecondsInstruction(float seconds)`, whose class summary already says "Pauses the agent for a fixed number of seconds" — adds nothing the signature didn't already say.
- ❌ `<param name="children">Instructions to run concurrently.</param>` on `ParallelInstruction(IEnumerable<InstructionBase> children)`, whose class summary already says "Runs all child instructions concurrently."
- ✅ Drop both; the signature plus class summary already carry the fact.
- Keep a `<param>` that states something the name doesn't promise: `<param name="epsilon">Ignored.</param>` is surprising — the name implies the value matters, and the doc reveals it doesn't. A short line that contradicts the reader's expectation earns its place; a short line that confirms it does not.

**Remarks: consequence, not implementation steps.**
- ❌ `Loop is bounded by SafetyIterations (64) to prevent infinite loops.`
- ✅ `Invariant: on degenerate graphs, always terminates after a finite number of node crossings.`

**Iterative-vs-recursive is mechanism, not consequence.** Don't narrate *how* stack-safety is achieved ("the loop iterates rather than recurses", "advances the cursor iteratively, never recursively") — state only the consequence a caller can observe, then stop.
- ❌ `Invariant: when a child completes synchronously inside its own Execute, the loop iterates rather than recurses — a long all-synchronous body cannot blow the call stack.`
- ✅ `Invariant: a long all-synchronous body cannot blow the call stack.`

Strip from `<remarks>`: internal method/field names as mechanisms, Unity Editor API calls (`Undo.RecordObject`), preprocessor symbols (`UNITY_EDITOR`), constant values, ordering details ("fires before X"), enumerating mutation categories ("no RNG draws, counter advances…"), why-the-code-does-it explanations, trailing `because…` clauses appended to otherwise-correct consequence statements ("this works because edge geometry is stored on the forward side" — drop the because clause), iteration-mechanism clauses ("iterates rather than recurses", "settles after a single rebuild, so hashing never loops" — keep the settles-after-one-rebuild fact, drop the "so it never loops" tail), `via <c>InternalMethod</c>` references (replace with the observable effect: "resets via `OnAfterDeserialize`" → "resets on scene reload"), and `as in <c>OtherMethod</c>` cross-references (state the behavioral fact directly). State the behavioral consequence instead.

**No trailing rationale or meta-labels on an invariant.** State the behavioral consequence and stop — this is the `so…` sibling of the `because…` rule above. Cut use-case justifications tacked on with `so…`: "the mark survives the last occupant leaving *so 'last car passed N seconds ago' selectors keep working*", "all nodes share this frame *so the path math works*" — the consequence stands alone; the use case belongs nowhere. Cut meta-prefixes and filler labels that announce an invariant instead of stating it: "*documented lookahead limitation —* lookahead is not handler-aware" → "lookahead is not handler-aware"; "Does not change state. *It is purely informational.*" → "side-effect-free — does not change state".

**No usage-advice or "wrap me" suffixes.** A docstring states what the member does, never how to deploy it. Cut tails that tell the reader to combine, wrap, pair, or bound this member with another: "pair with `RaceInstruction`/`TimeoutInstruction` for a guaranteed exit", "place this instruction last in any sequence", "bound it with a terminating compound", "Pair with `ExecuteFunctionInstruction` if you need movement effects". These are tutorial notes, not contract. Naming a sibling only as a foil is the same offense — "the teleport-to-path companion to `JoinNearestInstruction`, which performs a walk-in instead" → state this member's distinguishing behavior ("— no walk-in") and stop.

**Only claim "does not touch X" when it is a real contrast.** "agent state and speed are not touched" is signal on a member that *does* mutate some state — the reader learns what it spares (e.g. a movement instruction whose `Cancel` leaves the cursor put). It is noise on a pure observer: every wait/query/`Peek` leaves state alone, so stamping it on one observational instruction singles out an arbitrary one. Drop it from observational waits, proximity checks, and read-only queries.

**`logs a warning` is observable, not mechanism.** A `Debug.LogWarning` on degenerate input is user-visible behavior (the dev sees console output) and matches the fail-loud policy — document it like any other consequence; do **not** strip it as a Unity-API mechanism. The say-once rule still binds: a null-argument's "logs a warning and never completes" stated in a `<param>` is not restated in the method's `<remarks>` invariant — keep it on the `<param>` (most specific to that argument) and let the invariant carry only what the param cannot (e.g. "starts no coroutine").

**Say each fact once, at the most specific tag.** A fact stated in `<returns>` does not get restated in an `<remarks>` invariant; a `null`/`this` guard in `<returns>` does not get repeated in the `<param>` doc. The most common offender is a return-state mapping (`IsPlaced == true`, `IsValid == false`) spelled out in `<returns>` and then re-explained in an invariant — keep the full mapping in `<returns>`, and let the invariant carry only the *caller rule* ("detect 'no edge found' via `IsValid`, not `IsPlaced`"). The same one level up: a mapping (clamp-to-last, `-1`-on-empty) stated in a `Select` method summary is not re-spelled in the type's class-level `<remarks>` — drop the class-level copy. Sibling methods that share a contract (e.g. `ClosestCursor` / `FarthestCursor`) must phrase that shared contract identically — divergent wording for the same rule is a smell.

The same say-once rule extends three more ways: (1) **An invariant must not restate the `<summary>`.** If the summary already carries the fact ("regardless of nesting depth"), delete the invariant — don't rephrase it, and don't leave behind an empty `<remarks>`. (2) **An invariant must not restate a *neighboring* member's invariant.** When method A's `<remarks>` already documents a mode/contract, method B (e.g. `OnAfterDeserialize`) does not re-document it — its summary states its own effect once and stops. This includes a **callee**: a method does not re-document a contract already stated on a method it calls — a wake helper that delegates to a `Peek`/lookahead method does not restate that method's non-mutating or Peek-cache guarantees (`TryResume` → `PeekBranch`). (3) **An invariant must not restate itself.** One fact written twice — usually a second clause after a dash, semicolon, or period that re-says the first half in other words: "endpoint displacement is always zero — starts exactly at `from` and ends exactly at `to`"; "always unit length — normalized"; "completes synchronously — on the same frame"; "independent of `shape` — mutating one does not affect the other". Keep the crisper half, drop the echo. (4) **A `<param>` must not restate the class `<summary>` or a sibling property's `<summary>` for the same field.** `<param name="state">State to assign to the agent.</param>` on a ctor whose class summary already says "Sets the agent's state directly" is dropped. `<param name="to">Target speed.</param>` duplicating the `To` property's own summary is dropped from every overload's ctor, not just one. When two sibling overloads document the same parameter, check that neither is missing a fact the other states — fill the gap rather than deleting the richer one.

**Sibling identical-phrasing is not a "duplicate" to drop.** Sibling members that share a contract must phrase it identically (`JoinNearest` / `JoinFarthest` "no reachable edge" invariant) — each member keeps its own copy. Do not delete one as a restatement of the other; that is the opposite of the callee/neighbor rule above. Restatement-to-drop is one member echoing a *different* member it depends on or is documented by; identical sibling text is required, not redundant.

**Type-level docs do not describe per-parameter behavior.** A class / struct / enum `<summary>` or `<remarks>` states the type's purpose and load-bearing invariants — not what the type does with one specific serialized field or constructor argument. "`chaseForce` tunes identically regardless of mass" belongs on the `chaseForce` field, not the agent's class `<remarks>`. A cross-type parameter comparison ("tunes identically to the 3D variant") is also a stale reference — drop it. (Degenerate-input invariants like "`Capacity == 0` blocks every link" and contract guarantees like seed-reproducibility stay at type level — those characterize the type, not one tunable's behavior.)

**Plain words, not ornate ones.** Prefer the everyday term: `projects` not `drops a perpendicular`, `must keep` not `is responsible for keeping`, `staying on` not `holding on`. Cut filler enumerations ("branch 0… branch 1… and so on") and chains of three-plus em-dashes in one sentence — split into separate sentences or invariant lines. Fix grammar slips in param docs ("which edges direction" → "which edge directions").

**Plain words over textbook jargon.** Write the docstring for a reader who knows the codebase but not graph-theory or algorithm vocabulary. Prefer the everyday word for the concept: `visits each node once` not `settles each node`, `the search queue` not `the frontier`, `record the cheapest cost and re-queue it` not `relax the node`, `inserts X between A and B` not `splices X`, `links every node to every other` not `forms a complete graph`, `every node in one group links to every node in the other` not `bipartite graph`, `cost of finishing the edge` not `partial-edge cost`. When a precise technical term is genuinely load-bearing (e.g. the named algorithm in a method that implements it), state it once and then explain what it does in plain words — never lean on the term to carry the meaning. The same applies to delegate/callback tuples: spell roles out (`(neighbor, costToReachIt, cameFrom, viaLink)`) rather than terse math (`(n, c, p, v)`).

**Stale "allowed-overrides" lists.** When a base-class `<remarks>` enumerates what subclasses may override ("override `ShouldComplete` only", "use `ActiveChild` and override `ChooseChild` only"), that list must stay complete. Any compound holding child `InstructionBase` refs also overrides `Clone()` (child isolation), and a subclass that owns a coroutine/event also overrides `Cancel()` — the list must name them or not claim "only". Re-check the list against the actual subclasses before trusting it.

**`OnAfterDeserialize` / `OnBeforeSerialize` own summaries**: state the play-session effect once — `"Resets X so each play session starts fresh."` not `"Resets X when the scene loads or the selector is deserialized."` The method name already implies the trigger; naming it in the summary doubles the mechanism. Good: `Resets all cycle state so each play session begins with a fresh sequence.` Bad: `Resets all cycle state when the scene loads or the selector is deserialized.`

**No stale references.** Never write: "Used by X", "Called by X", "Shared by X", "Wired up by X", "Overridden by X", "in the current PR…", "matches OtherType", "companion to X", "same … as X", "the … body shared by X and Y", "to match the X contract" / "to match the constructor contract", "only called from method", "Defined explicitly so callers don't need to cast", "Identical to X, defined separately because…". These belong in commit messages, not docstrings. To restate: a cross-reference that names another member only to locate, compare, or justify this one is stale — state this member's own behavior directly and drop the reference (`"shares the Activate pipeline with Subscribe, so …"` → `"replanning re-runs the full activation path, so …"`).

**Protocol-interface exception.** Marker interfaces, change-notification events, and consumer-poll hooks (`IDynamicShape`, `IBranchSelector.MightYield`, empty markers like `IPerWaypointStateSelector`) exist only because a specific consumer needs them — lead with that consumer: `Polled by Waypoint.Update…`, `Signals to GraphValidator…`. Do not extend this to general-purpose interfaces (`IEdgeShape`, `IBranchSelector.Select`) whose consumers are many.

**`<inheritdoc/>`** when the override's behavior is fully described by the base. Write a full summary when the override: (a) installs or releases any external resource (coroutine, event, delegate slot, agent state), (b) has guard conditions not in the base contract, or (c) fires side effects the base doesn't describe. Add `<remarks>` only for invariants the base doesn't promise.

**Load-bearing constraints** (one-of, only-at-startup, must-cancel-children) → own `Invariant:` line, not buried in the summary.

**No upper bound on invariants.** Four real constraints → four lines. Concrete examples in interface docs are encouraged — stale lists are grep targets, not correctness bugs.

**Length** (>6 lines or any sentence >30 words): trim pass — did padding sneak in? Do two invariants restate each other?

**Doc must match the math.** Change a formula → update the docstring in the same edit. Re-derive from the body; do not copy the previous version.

**Reference template:** `Shapes/ShapeSampler.BuildLocalFrame` — read its docstring before writing any non-trivial method.

## STOP — self-check every docstring before moving on

1. **Summary**: purpose not mechanism? Exactly one sentence — no second clause, no trailing rationale? (Protocol-interface exception: consumer-mechanism may lead.)
2. **Params**: role-named (`forward` not `u`)? Could a reader write this same line from the signature and class summary alone, with zero new fact? Drop it — unless it states a surprising or non-obvious fact (an "epsilon" that's actually `Ignored`), which earns its line precisely because it isn't implicit.
3. **Invariants**: each states user-visible *consequence*, not implementation step?
4. **Code refs**: all field/type/literal mentions in `<c>` tags?
5. **Math**: re-derive any formula claim from the body — mismatch → fix both in same edit.
6. **Load-bearing constraints** (one-of, only-on-startup, must-cancel-children): own `Invariant:` line?
7. **`<inheritdoc/>`**: override adds resources / guards / side effects not in base → write full summary. Override *diverges* from base behavior (does something the inherited summary describes wrongly) → replace `<inheritdoc/>`, don't keep it.
8. **Say-once**: is any fact in two tags (`<returns>` + invariant, `<param>` + `<returns>`, `<param>` + class `<summary>`, `<param>` + a sibling property's `<summary>` for the same field, `Select` summary + class `<remarks>`), restating the summary, or restating a callee/neighbor's documented contract? Collapse to the most specific one. (Identical *sibling* text is required, not a duplicate.)
9. **Within-invariant echo**: a clause after a dash/semicolon/period that re-says the first half ("unit length — normalized", "synchronously — same frame")? Keep the crisper half.
10. **Type-level scope**: does a class/enum `<summary>`/`<remarks>` describe what the type does with one specific field or ctor arg? Move it to that field's doc.
11. **No deploy-advice / foil refs**: any "pair with X", "wrap in X", "place last", "companion to X"? Cut — state behavior, not how to use it.
12. **`does not touch X`**: real contrast (member mutates something else) or noise on a pure observer? Drop if observer.
13. **Plain words**: ornate verb (`drops a perpendicular`), fluff (`is responsible for`), filler enumeration (`and so on`), 3+ em-dash chains, grammar slip, or textbook jargon (`settle`/`frontier`/`relax`/`splice`/`complete graph`/`bipartite`)? Plain-out, split, and explain any load-bearing technical term in everyday words.
14. **Length**: >6 lines or >30-word sentence → trim. Padding? Duplicate invariants?
