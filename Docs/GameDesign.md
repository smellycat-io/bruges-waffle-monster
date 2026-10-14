# Bruges Waffle Monster

## Foundational Architecture

- The game loop is represented by `RunnerState`, `KitchenApproachState`, and `ArenaState`, each implementing `IGameState` and managed by `GameStateManager`.
- State transitions are currently manual debug transitions only; gameplay trigger conditions are intentionally unspecified.
- `WaffleWallet` is the shared component for waffle capacity and hit-strength removal, used by both citizens and the player. Its `Drained` event now drives citizen disengage behavior (see "Citizen Enemies" below).

## Player Movement (feature/player-movement)

Side-view 2D platformer control. The player fully controls movement — there is no
auto-scroll: run left/right, jump, and automatically climb any surface flagged as
climbable.

- Movement is a **player-local** state machine (`PlayerMovementController`): `Grounded`,
  `Airborne` (covers both the rising jump and the fall), `Climbing`. This is separate
  from `GameStateManager`'s Runner / KitchenApproach / Arena flow — that tracks where
  the player is in the level; this tracks what the body is doing.
- Input is isolated behind `IPlayerInputSource`. `TapPlayerInput` holds the intent
  (`HorizontalAxis`, `ClimbAxis`, a queued jump) and is all the movement code sees. Two
  driver components feed it, and both run at once:
  - `ScreenTapZoneInput` — run + jump. **Hold the left / right half of the screen** to
    run that way; run does **not** start until the touch is held past the tap-vs-hold
    threshold, so a **quick tap** (either half) is a pure jump with zero run drift. The
    zone/timing logic is the testable `TapZoneInterpreter` (`Tick(time)` promotes a held
    press to a run).
  - `ClimbDragInput` — the climb axis. **Touch anywhere and drag up / down** to move up /
    down the wall; **release to stop** (hold-to-move, no momentum); no touch = hang. The
    drag model (anchor + deadzone, displacement sign) is the testable
    `ClimbDragInterpreter`.
  Any other driver calling the same hooks swaps in without touching `TapPlayerInput` or
  the state machine.
- **Climbing is automatic.** Entering contact with a collider carrying a
  `ClimbableSurface` component forces the `Climbing` state and disables gravity. On the
  wall, movement is **strictly vertical and entirely input-driven** — `climbInput` (+1
  up / 0 hang / -1 down) sets the vertical velocity directly; there is no auto-ascent and
  no residual velocity when you release. Run input is ignored here.
  - **Jump → wall-jump**: kicks off into `Airborne` with a horizontal push away from the
    wall's side plus an upward impulse (`WallJumpVelocity`). Still works while dragging.
    A short "moving away from the wall" check stops the player instantly re-grabbing the
    wall they just left.
  - **Holding run input away from the wall while climbing detaches with no impulse at
    all** — an unassisted fall from wherever you let go, gravity taking over immediately.
    Separate from wall-jump: this needs no jump press and gives no horizontal kick, just
    lets go. The same "moving away from the wall" check that protects wall-jump from an
    instant re-grab covers this case too, for free.
  - Leaving contact (or climbing off the top) hands control back to run/jump.
  Plain colliders are never climbable.
- **Fixed: holding a direction into a plain (non-climbable) wall mid-air used to stick the
  player in place instead of falling.** Root cause, confirmed by direct physics
  measurement: this controller re-applies horizontal velocity into the `Rigidbody2D` every
  `FixedUpdate` regardless of collisions, and Unity's *default* 2D collider friction is
  non-zero — holding into a wall keeps re-creating a contact against it, and friction at
  that same contact was also resisting the player's vertical *fall*, not just the
  horizontal push. The player would hang frozen mid-fall against any solid surface, not
  just climbable ones. Fixed by giving every player `Collider2D` a zero-friction
  `PhysicsMaterial2D` in `PlayerController.Awake()`; climbing itself is driven entirely by
  explicit velocity (see above), never friction, so this has no effect on climb feel — only
  on incidental contact with non-climbable geometry.

### Player movement — balance questions (PLACEHOLDER, need sign-off)

Movement values live in `PlayerMovementConfig` (ScriptableObject,
`Assets/ScriptableObjects/Player/`); input knobs are serialized fields on the driver
components. All first-pass "does it work" guesses, **not** final feel:

| Value | Placeholder | Where | Notes |
|-------|-------------|-------|-------|
| Run speed | 6 u/s | `PlayerMovementConfig` | same on ground and in the air |
| Jump velocity | 12 u/s | `PlayerMovementConfig` | instant take-off velocity, not a target height |
| Climb speed | 3 u/s | `PlayerMovementConfig` | constant, up and down |
| Wall-jump velocity | (8, 11) | `PlayerMovementConfig` | x = push away from wall, y = upward impulse |
| Gravity scale | 3 | `PlayerMovementConfig` | Rigidbody2D multiplier while not climbing (project gravity −9.81) |
| Tap-vs-hold threshold | 0.18 s | `ScreenTapZoneInput` | held longer = run; released sooner = jump |
| Left-zone fraction | 0.5 | `ScreenTapZoneInput` | share of screen width that is the "run left" zone |
| Climb-drag deadzone | 14 px | `ClimbDragInput` | drag distance from the touch-down point before climbing starts |

Open questions beyond the raw numbers:

- **Climb-drag mapping** — the climb axis is the sign of (finger Y − touch-down anchor Y)
  past the deadzone, so you can hold above the anchor to keep ascending. Alternative
  models if this feels off: frame-to-frame delta (must keep moving), or a fixed
  virtual-slider zone. Flagged, not silently decided.
- **Tap-vs-hold cost** — deferring run-start to the threshold means an intentional run
  and every direction switch (lift one half, press the other) has ~180 ms latency. This
  was the chosen tradeoff (kills jump-tap drift) but is worth confirming in feel-testing.
- **Wall-jump horizontal travel** — air x-velocity is input-driven, so a neutral-input
  wall-jump only pushes a short distance before x snaps back to 0; holding *away* from
  the wall after the kick carries much further. May want real horizontal momentum/decay
  in the air instead.
- **Jump**: fixed impulse only — no variable jump height, no coyote-time, no jump
  buffering.
- **Climb**: no sideways dismount, no climb-off onto the ground floor (you detach by
  stepping away from the wall), no climb-speed ramp.
- **Air control**: full run-speed steering mid-air (no reduced air acceleration).
- **Ground check**: 0.15 u circle at a "GroundCheck" child; may need per-character
  tuning once real art/colliders exist.

## Citizen Enemies (feature/citizen-enemies)

Three citizen types share one behavior component (`CitizenAI`), parameterized entirely by a
`CitizenTypeData` asset per type (`Assets/ScriptableObjects/Enemies/Citizen_*.asset`) — a new
citizen type is a new asset, never a new script.

- **Detection is line-of-sight, not proximity.** Each tick, `CitizenAI` raycasts from itself
  to the player; if nothing solid is in the way and the player is within `DetectionRange`,
  it chases. Anything else the ray hits first (a building, another citizen, ...) blocks it —
  there's no separate "Obstacle" tag/layer to remember to apply to new geometry, any collider
  in the way counts. Losing line-of-sight (player breaks the ray) stops the chase immediately
  — no memory/last-known-position pursuit.
- **Chase movement is grounded and horizontal-only** (citizens don't climb or match the
  player's height) — a deliberate choice so climbing stays a real escape route. Not
  explicitly requested; flagging in case full 2D pursuit was actually intended.
- **Idle wander**: while Idle (not Chasing, not Disengaged), a citizen wanders randomly
  within `WanderRadius` of its own spawn point rather than standing still — pick a random
  point, walk to it at `WanderSpeed`, pause `WanderPauseDuration` seconds, repeat.
  - **Horizontal-only, same reasoning as chase**: citizens are gravity-less kinematic
    bodies (see above), so a vertical wander offset would leave one permanently floating —
    wander only ever picks a new x position, never y.
  - **Lives in the existing split, not a new parallel system**: `CitizenWanderController`
    is a new plain class (no MonoBehaviour, no `Time.x` reads), but it's owned and ticked
    by `CitizenAI` exactly the way `ClimbableContactTracker` and `CitizenBehaviorController`
    already are — same single `FixedUpdate`, same "plain class decides, MonoBehaviour
    glue moves the Rigidbody2D" split used everywhere else in this codebase. `ApplyState`'s
    existing per-state switch just gained an `Idle` case (`WanderTowardTarget`) alongside
    the existing `Chasing`/`Disengaged` ones.
  - **Stops the instant Chasing starts, resumes fresh (not from spawn) the instant Chasing
    ends**: `CitizenWanderController.Tick` is fed `isActive = (state == Idle)` every tick
    regardless of state, so going inactive clears any in-progress target immediately (no
    delay to chase responsiveness — movement is picked by the same switch statement, so
    there's no way for wander and chase to run the same tick), and reactivating discards
    any stale target/pause and picks a fresh point right away from wherever the citizen
    currently is. The radius is still measured from the *original* spawn point, not
    wherever the chase ended — a citizen chased far from home wanders back toward its own
    territory rather than adopting a new one.
  - **Wander speed is its own `CitizenTypeData` field, not a hardcoded fraction of
    `MoveSpeed`** — so a designer can tune each type's wander pace independently of its
    chase speed without touching code (e.g. a Vendor that barely strays from its stall but
    still chases at a normal clip). `WanderRadius` and `WanderPauseDuration` are likewise
    per-type fields, since a Guard patrolling wider than a Tourist window-shopping is a
    plausible personality difference worth exposing to feel-testing, not baking in.
- **Catching the player** (trigger contact while Chasing) calls `PlayerStrikeSystem.RegisterStrike`
  on the player — `CitizenAI` never touches the player's wallet directly. A Disengaged
  (crying) citizen can't catch the player even on contact.
- **Wallet drain → disengage** uses the citizen's own `WaffleWallet.Drained` event (the "shared
  component, drained behavior now implemented" piece GameDesign.md previously flagged as
  outstanding). Once Disengaged it's a one-way latch for the rest of the level.
- **The player-side strike/catch system** (`PlayerStrikeSystem`, `Assets/Scripts/Player/Strike/`)
  is a separate component from `PlayerController` (movement vs. encounter-consequences are
  different responsibilities) and separate from the Chef's eventual instant-loss system —
  a strike always keeps whatever remains in the stash and respawns the player; it never
  resets the level. Strike 1 removes `hitStrength` waffles, strike 2 removes
  `hitStrength × 2`, strike 3 (and any catch after that, regardless of which type causes it)
  wipes the waffle wallet entirely and fires a `FullStashWiped` event. **There is no topping
  stash system yet** (Toppings/Cooking are unbuilt) — `FullStashWiped` is the hook for it to
  also clear itself once it exists; nothing invents that logic here. `ResetStrikes()` exists
  for a full level restart (e.g. the Chef's instant-loss) to call once that system exists —
  nothing calls it yet.
- **Respawn invincibility**: after a strike, the player is immune to further catches for
  `invincibilityDuration` seconds (a serialized field on `PlayerStrikeSystem`, PLACEHOLDER —
  see table below) — a catch during that window is ignored entirely: no strike count, no
  wallet loss, no re-respawn. A flicker (the sprite's `SpriteRenderer.enabled` toggling,
  found on a child if not wired explicitly) is the visual tell so it's never an invisible
  rule. The countdown and flicker are driven by `PlayerStrikeSystem.Tick(deltaTime)` — called
  from `Update` in play, and directly with synthetic values in EditMode tests, the same
  "logic takes an explicit time/step, doesn't read `Time.x` itself" pattern already used by
  `PlayerMovementController.Tick` and `TapZoneInterpreter.Tick`.
- **Respawn position** = wherever the player's `PlayerStrikeSystem` was at `Awake()` (i.e. the
  player's placed starting position for this scene). There's no dedicated level-start marker
  yet; if levels ever get mid-level checkpoints, this needs a real spawn-point component
  instead of "wherever you started."
- **Fall damage feeds the same strike system as a citizen catch** (`PlayerStrikeSystem.RegisterFall`)
  — falling is not a separate life-loss mechanic. `PlayerController` tracks the peak height
  reached since the player last left the ground (`FallTracker`, a plain class mirroring the
  `Tick`-driven pattern above) and reports the completed drop the instant the player lands.
  Falls at or under `fallDamageThreshold` are free. Past it, `RegisterFall` calls
  `RegisterStrike` — same ×1/×2/full-wipe escalation, same respawn, same invincibility window
  as a catch, and a fall during invincibility is ignored exactly like a catch would be (an
  emergent consequence of reusing `RegisterStrike` outright, not a separately-decided rule).
  - **Only `Airborne` time counts.** Climbing — including deliberately climbing all the way
    back down a wall — never accrues fall distance, even from a great height; grabbing a wall
    mid-fall "catches" the fall with no penalty, and only the distance *after* letting go
    again would count.
  - **Hit-strength scales with how far past the threshold the fall was** — proposed formula,
    not requested by name and flagged here for sign-off rather than silently assumed:
    `hitStrength = clamp(1 + floor((fallDistance − threshold) / scaleDistance), 1, maxHitStrength)`.
    That is, 1 hit-strength as soon as you're over the threshold, +1 for every further
    `scaleDistance` units, capped at `maxHitStrength` so no single fall can be worse than the
    hardest citizen catch (Guard, hit-strength 3).
- **Climbing is opt-in per citizen type** via `CitizenTypeData.CanClimb` (a data flag, not a
  type check in `CitizenAI`) — currently only the Guard has it. A climbing citizen reuses the
  player's own `ClimbableSurface` marker + `ClimbableContactTracker` for "am I touching
  climbable geometry," then does a simple vertical follow toward the target's height at
  `MoveSpeed` (no drag input, no wall-jump — the task explicitly didn't need the player's full
  climb model here, and citizens are kinematic so there's no gravity to fight in the first
  place). Tourist/Vendor are unaffected — same strictly-horizontal chase as before.
- **Placeholder visuals**: `CitizenTypeData.PlaceholderColor` tints the sprite per type
  (Tourist yellow, Vendor orange, Guard blue); a Disengaged citizen's tint darkens
  (`disengagedColorMultiplier` on `CitizenAI`, presentation only, not a balance number).
- Build/playtest via **Tools ▸ Bruges Waffle Monster ▸ Build Citizen Sandbox** (after the
  player sandbox) — 2 of each type plus one opaque obstacle block for line-of-sight testing.

### Citizen balance — wallet & hit-strength are LOCKED, movement/detection are placeholders

| Type | Wallet | Hit strength | Move speed (PLACEHOLDER) | Detection range (PLACEHOLDER) | Can climb | Color |
|------|--------|--------------|---------------------------|-------------------------------|-----------|-------|
| Tourist | 1–2 | 1 | 2 u/s | 4 u | No | soft yellow |
| Vendor  | 3–4 | 2 | 3.5 u/s | 5 u | No | orange |
| Guard   | 5–6 | 3 | 5 u/s | 7 u | **Yes** | dark blue |

Speeds are chosen relative to the player's 6 u/s run speed (Guard deliberately stays just
below it — outrunnable in a straight sprint, but only barely). Detection ranges are a first
guess with no real reference point. Both need feel-testing; wallet size and hit strength are
final per direction, not to be re-guessed.

| Type | Wander radius (PLACEHOLDER) | Wander speed (PLACEHOLDER) | Wander pause (PLACEHOLDER) |
|------|------------------------------|------------------------------|-------------------------------|
| Tourist | 3 u | 1 u/s | 2.5 s |
| Vendor  | 2.5 u | 1.5 u/s | 3 s |
| Guard   | 4 u | 2 u/s | 1.5 s |

First-pass personality guesses, not balance-critical like wallet/hit-strength: Vendor stays
closest to its stall but pauses longest (tending it); Guard patrols the widest area at the
briskest pace but pauses shortest (steady patrol vs. window-shopping/browsing). All three
columns need feel-testing sign-off same as move speed/detection range above.

| Value | Placeholder | Where | Notes |
|-------|-------------|-------|-------|
| Respawn invincibility duration | 1.5 s | `PlayerStrikeSystem` | needs feel-testing sign-off |
| Invincibility flicker interval | 0.12 s | `PlayerStrikeSystem` | presentation only, not balance |
| Fall damage threshold | 8 units | `PlayerStrikeSystem` | falls at/under this are free; needs feel-testing sign-off |
| Fall damage scale distance | 4 units | `PlayerStrikeSystem` | +1 hit-strength per this many units past the threshold; needs sign-off |
| Fall damage max hit-strength | 3 | `PlayerStrikeSystem` | caps a fall at the Guard's hit-strength; needs sign-off |

### Other open questions

- ~~No catch cooldown~~ — resolved by respawn invincibility: a citizen standing on/near the
  spawn point can no longer immediately re-strike, for `invincibilityDuration` seconds.
- Citizen chase movement doesn't decelerate/arrive — it can jitter right at contact distance
  for a frame or two before the catch registers. Cosmetic only.
- **Does a `ClimbableSurface` block citizen line-of-sight?** Currently yes — the LOS raycast
  doesn't special-case it, so a climbable wall (even though it's a "thin decorative" trigger)
  counts as an obstacle like any other collider. Not exercised in normal play yet, but worth
  a conscious call once real building geometry exists: a Guard climbing the *same* wall the
  player is on would currently still need clear sight past the wall's own collider to have
  spotted them in the first place.
- The player's sandbox starting wallet (20 waffles, in `PlayerSandboxBuilder`) is a
  play-testing convenience, not a real starting-stash balance decision.
- ~~Chef known-return windows, random-return probability, bedtime, blink-rate curve, and
  fake-out frequency~~ — resolved below (Chef Schedule & Warning System).
- Waffle removal scaling beyond the current direct hit-strength prototype.

## Chef Schedule & Warning System (feature/chef-warning-system)

The Chef cycles between **Away** from the kitchen hot zone (safe) and **Present** in it
(an instant, total level restart if the player is caught inside), plus a one-time,
extended **Bedtime** safe window late in the level. Three independently-testable warning
components — Security Monitor (blink), Silhouette, Audio Cue — all read the same
`ChefController`/`ChefScheduleController`, but differ in reliability. This runs as its own
parallel state machine (`ChefScheduleController`, a plain class ticked by `ChefController`),
**not** an `IGameState` — it keeps ticking regardless of Runner/Kitchen-Approach/Arena.

- **Schedule semantics** (interpreting the placeholder `ChefScheduleData` fields — flagged
  rather than assumed silently): `knownReturnWindows` is a list of (min, max) away-duration
  ranges, cycled in order and looped — each away period's actual length is randomly drawn
  from whichever range is current. This is the "learnable rhythm" half. `randomReturnProbability`
  (rolled per second while safely Away) can start a real return countdown early, unpredictably
  — the "breaks the pattern" half. Either path starts the same `returnWarningLeadTime`-long
  countdown before the Chef actually becomes Present.
- **Bedtime** is an elapsed-level-time threshold (`bedtime`), not a duration — the first Away
  period that would otherwise start after that point becomes one extended `bedtimeDuration`-long
  safe window instead. **Decision: after bedtime ends, normal cycling resumes** (the
  alternative — staying safe for the rest of the level — was considered and declined).
- **Added fields** not in the original placeholder SO, needed to make the schedule actually
  loop/behave, all PLACEHOLDER: `returnWarningLeadTime`, `presentDuration` (how long the Chef
  stays Present before leaving again — not called out explicitly in the original note but
  required for the cycle to continue), `bedtimeDuration`, `fakeOutRampDuration`,
  `fakeOutPeakRange` (a fake-out ramps to a random peak in this 0–1 range, never guaranteed to
  look like a full/imminent real return, then resets — never assumed reset vs. hold-at-peak
  either way, decided as reset), `arenaEntryCountdownExtension`.
- **Security Monitor** (`SecurityMonitorWarning` + `BlinkRateCalculator`): blink rate follows
  `blinkRateCurve` evaluated at `ChefController.BlinkRampProgress01` — a single 0–1 ramp signal
  that looks identical whether it's driven by a real return countdown or a fake-out. This
  component has no way to distinguish the two, by design. Fake-out frequency is
  `fakeOutFrequency` (average per minute) converted to a per-second probability, rolled only
  while safely Away with no countdown already active.
- **Silhouette** (`SilhouetteWarning`): fires only on `ChefScheduleController.RealReturnMidpointReached`
  — an event the schedule guarantees never fires during a fake-out and always fires exactly
  once per real countdown, at its midpoint. The one signal the player can always trust.
- **Audio Cue** (`AudioCueWarning`): volume/pitch ramp with proximity to an actual return
  (`ComputeProximity`, a pure/static method for testability), same reliability as the
  Silhouette. **Decision: no audio fake-out** — a subtle audio fake-out was raised as an
  option and declined for this pass; only the blink layer is ever unreliable.
- **Kitchen hot zone**: a new concept, not previously in this doc. `KitchenHotZone` is a
  trigger-collider marker (placed on level geometry, mirroring `ClimbableSurface`) reporting
  player enter/exit to `ChefEncounter`, which alone decides the consequence. Being inside it
  the instant the Chef is/becomes Present (`ChefEncounter`) reloads the whole scene — no
  partial-reset plumbing needed anywhere else, since everything (wallet, strikes, citizen
  state, the Chef's own schedule) comes back fresh because the scene does. Deliberately
  separate from `PlayerStrikeSystem`'s respawn-with-remainder flow; does not call
  `PlayerStrikeSystem.ResetStrikes()` since a scene reload makes that redundant.
- **Kitchen door** (`KitchenDoorTrigger`): reaching it while the Chef is away (including
  mid-return-warning or Bedtime) calls `GameStateManager.TransitionToArena()`. The Arena
  itself is a same-scene room (`ArenaRoomMarker`, starts inactive, toggled by `ArenaState`)
  rather than a separately loaded scene — no scene-loading infrastructure exists in this
  project yet, and building one was out of scope here. Contains only a `WaffleIronMarker`
  positional placeholder — no attack timing, damage, or fight logic (explicitly out of scope
  for this branch).
- **Arena entry timer extension**: the moment `ArenaState.Enter()` runs, it adds
  `arenaEntryCountdownExtension` seconds to the Chef's current/next return countdown via
  `ChefController.ExtendReturnCountdown` — chosen over pausing the schedule outright, per the
  brief, so the Waffle Iron fight has breathing room without removing the threat entirely.

### Chef schedule — all PLACEHOLDER, needs feel-testing

| Value | Placeholder | Notes |
|-------|-------------|-------|
| Known away-duration ranges | not set (empty) | needs at least one (min,max) range authored per level before this does anything meaningful |
| Random early-return probability | 0/s | per-second chance during a safe Away stretch |
| Return warning lead time | 4 s | length of every real return countdown |
| Present duration | 8 s | how long the Chef stays in the hot zone before leaving again |
| Bedtime start | 0 s | elapsed level-time threshold; 0 effectively disables bedtime until authored |
| Bedtime duration | 30 s | length of the extended safe window |
| Fake-out frequency | 2/min | average fake-out ramps per minute while safely Away |
| Fake-out ramp duration | 2 s | time to rise to its (random) peak before resetting |
| Fake-out peak range | 0.3–0.9 | a fake-out never guarantees full ramp intensity |
| Arena entry countdown extension | 15 s | added to the Chef's return countdown the instant the Arena begins |
