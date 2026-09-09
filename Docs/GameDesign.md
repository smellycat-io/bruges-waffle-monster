# Bruges Waffle Monster

## Foundational Architecture

- The game loop is represented by `RunnerState`, `KitchenApproachState`, and `ArenaState`, each implementing `IGameState` and managed by `GameStateManager`.
- State transitions are currently manual debug transitions only; gameplay trigger conditions are intentionally unspecified.
- `WaffleWallet` is the shared component for waffle capacity and hit-strength removal. It exposes a drained state and event; drained behavior is intentionally not implemented yet.

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

## Balance Questions

The following values remain placeholders and need design decisions before they are meaningful:

- Citizen wallet size, hit strength, and speed.
- Chef known-return windows, random-return probability, bedtime, blink-rate curve, and fake-out frequency.
- Waffle removal scaling beyond the current direct hit-strength prototype.
