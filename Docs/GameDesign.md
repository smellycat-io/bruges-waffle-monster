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
  - Leaving contact (or climbing off the top) hands control back to run/jump.
  Plain colliders are never climbable.

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
- **Respawn position** = wherever the player's `PlayerStrikeSystem` was at `Awake()` (i.e. the
  player's placed starting position for this scene). There's no dedicated level-start marker
  yet; if levels ever get mid-level checkpoints, this needs a real spawn-point component
  instead of "wherever you started."
- **Placeholder visuals**: `CitizenTypeData.PlaceholderColor` tints the sprite per type
  (Tourist yellow, Vendor orange, Guard blue); a Disengaged citizen's tint darkens
  (`disengagedColorMultiplier` on `CitizenAI`, presentation only, not a balance number).
- Build/playtest via **Tools ▸ Bruges Waffle Monster ▸ Build Citizen Sandbox** (after the
  player sandbox) — 2 of each type plus one opaque obstacle block for line-of-sight testing.

### Citizen balance — wallet & hit-strength are LOCKED, movement/detection are placeholders

| Type | Wallet | Hit strength | Move speed (PLACEHOLDER) | Detection range (PLACEHOLDER) | Color |
|------|--------|--------------|---------------------------|-------------------------------|-------|
| Tourist | 1–2 | 1 | 2 u/s | 4 u | soft yellow |
| Vendor  | 3–4 | 2 | 3.5 u/s | 5 u | orange |
| Guard   | 5–6 | 3 | 5 u/s | 7 u | dark blue |

Speeds are chosen relative to the player's 6 u/s run speed (Guard deliberately stays just
below it — outrunnable in a straight sprint, but only barely). Detection ranges are a first
guess with no real reference point. Both need feel-testing; wallet size and hit strength are
final per direction, not to be re-guessed.

### Other open questions

- No catch cooldown: a citizen can register a strike again immediately if it ends up
  overlapping the player again right after a respawn (e.g. spawn point near a citizen).
  Not handled — flag if this becomes a real problem in testing.
- Citizen chase movement doesn't decelerate/arrive — it can jitter right at contact distance
  for a frame or two before the catch registers. Cosmetic only.
- The player's sandbox starting wallet (20 waffles, in `PlayerSandboxBuilder`) is a
  play-testing convenience, not a real starting-stash balance decision.
- Chef known-return windows, random-return probability, bedtime, blink-rate curve, and fake-out frequency.
- Waffle removal scaling beyond the current direct hit-strength prototype.
