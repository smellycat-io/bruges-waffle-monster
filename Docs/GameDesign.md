# Bruges Waffle Monster

## Foundational Architecture

- The game loop is represented by `RunnerState`, `KitchenApproachState`, and `ArenaState`, each implementing `IGameState` and managed by `GameStateManager`.
- State transitions are currently manual debug transitions only; gameplay trigger conditions are intentionally unspecified.
- `WaffleWallet` is the shared component for waffle capacity and hit-strength removal. It exposes a drained state and event; drained behavior is intentionally not implemented yet.

## Balance Questions

The following values remain placeholders and need design decisions before they are meaningful:

- Citizen wallet size, hit strength, and speed.
- Chef known-return windows, random-return probability, bedtime, blink-rate curve, and fake-out frequency.
- Waffle removal scaling beyond the current direct hit-strength prototype.
