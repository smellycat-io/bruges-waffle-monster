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
- Input is fully **tap-based** and isolated behind `IPlayerInputSource`. `TapPlayerInput`
  holds the intent (`HorizontalAxis` + `ConsumeJumpRequest`) and is all the movement
  code sees; `ScreenTapZoneInput` is the default driver: **hold the left / right half of
  the screen** to run that way, **quick-tap either half** to jump. Zone split and the
  tap-vs-hold time threshold live on `ScreenTapZoneInput`; the zone/timing logic is the
  testable `TapZoneInterpreter`. Any other driver that calls the same `Press*/Release*`
  hooks swaps in without touching `TapPlayerInput` or the state machine.
- **Climbing is automatic.** Entering contact with a collider carrying a
  `ClimbableSurface` component forces the `Climbing` state and disables gravity. On the
  wall, movement is strictly vertical:
  - No run input held → automatic **ascent** at climb speed.
  - **Any** run direction held → **descent** at climb speed. (See open question on this
    mapping below.)
  - **Jump → wall-jump**: kicks off into `Airborne` with a horizontal push away from the
    wall's side plus an upward impulse (`WallJumpVelocity`). A short "moving away from
    the wall" check stops the player instantly re-grabbing the wall they just left.
  - Leaving contact (or climbing off the top) hands control back to run/jump.
  Plain colliders are never climbable.

### Player movement — balance questions (PLACEHOLDER, need sign-off)

Movement values live in `PlayerMovementConfig` (ScriptableObject,
`Assets/ScriptableObjects/Player/`); the input threshold lives on the
`ScreenTapZoneInput` component. All first-pass "does it work" guesses, **not** final feel:

| Value | Placeholder | Notes |
|-------|-------------|-------|
| Run speed | 6 u/s | full-tilt horizontal speed, same on ground and in the air |
| Jump velocity | 12 u/s | instant take-off velocity, not a target height |
| Climb speed | 3 u/s | constant, both up and down |
| Wall-jump velocity | (8, 11) | x = push away from wall, y = upward impulse |
| Gravity scale | 3 | Rigidbody2D gravity multiplier while not climbing (project gravity −9.81) |
| Tap-vs-hold threshold | 0.18 s | on `ScreenTapZoneInput`; touch released faster than this = jump, longer = run hold |
| Left-zone fraction | 0.5 | on `ScreenTapZoneInput`; share of screen width that is the "run left" zone |

Open questions beyond the raw numbers:

- **Climb-down direction mapping** — because there is no dedicated "down" input, holding
  *either* run direction while on the wall drives the descent (release = ascend). This
  means you can't hold a direction to "hang" in place. If that feels wrong, the
  alternative is to only descend while holding *toward* the wall (and treat
  *away* as a detach/step-off). Flagged, not silently decided.
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
