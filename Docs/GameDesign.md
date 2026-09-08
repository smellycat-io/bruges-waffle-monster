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
- Input is fully **tap-based** and isolated behind `IPlayerInputSource`
  (`TapPlayerInput` = hold-to-run left/right + tap-to-jump; on-screen buttons by
  default, swappable for full-screen tap zones without code changes). The movement
  logic never reads input devices directly, so controls can be re-tuned later in
  isolation.
- **Climbing is automatic.** Entering contact with a collider carrying a
  `ClimbableSurface` component forces the `Climbing` state, disables gravity, and moves
  the player straight up at climb speed — no jump or directional input. Leaving contact
  (including climbing off the top) hands control straight back to run/jump. Plain
  colliders are never climbable.

### Player movement — balance questions (PLACEHOLDER, need sign-off)

All values live in `PlayerMovementConfig` (ScriptableObject,
`Assets/ScriptableObjects/Player/`) and are first-pass "does it work" guesses, **not**
final feel:

| Value | Placeholder | Notes |
|-------|-------------|-------|
| Run speed | 6 u/s | full-tilt horizontal speed, same on ground and in the air |
| Jump velocity | 12 u/s | expressed as instant take-off velocity, not a target height |
| Climb speed | 3 u/s | constant, no acceleration |
| Gravity scale | 3 | Rigidbody2D gravity multiplier while not climbing (project gravity −9.81) |

Open questions beyond the raw numbers:

- **Jump**: fixed impulse only — no variable jump height, no coyote-time, no jump
  buffering yet.
- **Climb**: vertical only — no sideways dismount, no climb-down, no jump-off-wall,
  no climb speed ramp.
- **Air control**: currently full run-speed steering mid-air (no reduced air
  acceleration).
- **Ground check**: 0.15 u circle at a "GroundCheck" child; may need per-character
  tuning once real art/colliders exist.

## Balance Questions

The following values remain placeholders and need design decisions before they are meaningful:

- Citizen wallet size, hit strength, and speed.
- Chef known-return windows, random-return probability, bedtime, blink-rate curve, and fake-out frequency.
- Waffle removal scaling beyond the current direct hit-strength prototype.
