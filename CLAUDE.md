# Bruges Waffle Monster — Project Standards

This file is read automatically by Claude Code at the start of every session in this
project. Keep it up to date as decisions change — it's the source of truth for how
this codebase should be built, not just a one-time note.

## Project Overview

A 2D side-scrolling runner mobile game (iOS/Android) built in Unity (2D URP template).
You play a waffle monster stealing waffles and toppings from a Bruges-themed city.
Full game design context lives in `/Docs/GameDesign.md` (create/maintain this as
design decisions are finalized — see "Design Doc Sync" below).

- **Engine**: Unity (2D URP template), LTS version
- **Language**: C#
- **Target platforms**: iOS, Android
- **Script editor**: VS Code (Unity opens scripts here, but Claude Code should assume
  Unity itself is the source of truth for scenes/prefabs/assets)
- **Unity MCP**: connected — Claude Code can inspect/modify live scene state, not just
  script files. Prefer using MCP scene inspection to confirm GameObject/component
  state before assuming what's in a scene.

## Folder Structure

All game code lives under `Assets/`. Use this structure and keep it flat/predictable
rather than nesting deeply:

```
Assets/
  Scripts/
    Core/            # GameStateManager, scene/state transition logic, singletons (rare, see below)
    Player/           # Waffle monster movement, controls, animation hooks
    Enemies/          # Citizen types, Chef, Waffle Iron — shared + per-type behavior
    Waffles/          # WaffleWallet, waffle pickup/drop logic, waffle data
    Toppings/         # Topping pickups, throw/attack logic, topping data
    Cooking/          # Eat Event / stash / recipe system (post-boss unlock)
    UI/               # HUD, menus, end-of-level screen
    Data/             # ScriptableObject definitions (see "Tunable Values" below)
  Prefabs/
    Enemies/
    Pickups/
    UI/
  ScriptableObjects/  # actual .asset instances (data), separate from the Data/ scripts that define them
    Enemies/
    Recipes/
    Waffles/
  Scenes/
    Runner_Level1.unity
    (one scene per level; boss/kitchen arenas are separate additive scenes or scenes
    loaded via the state manager — decide this concretely when we build the state
    machine, and update this section once decided)
  Art/                # existing Bruges Waffles assets
  Audio/
Docs/
  GameDesign.md       # living design doc, kept in sync with this file (not under Assets/)
```

If a new system doesn't obviously fit an existing folder, stop and ask rather than
inventing a new top-level folder ad hoc.

## Naming Conventions

- **Classes, methods, public fields, properties**: PascalCase (`WaffleWallet`,
  `TakeHit()`, `CurrentWaffleCount`)
- **Private fields**: camelCase with leading underscore (`_currentWaffles`,
  `_maxStashCapacity`)
- **Local variables, parameters**: camelCase (`hitStrength`, `waffleAmount`)
- **Constants**: PascalCase or ALL_CAPS is fine, but be consistent within a file —
  prefer PascalCase (`const int MaxCitizenTypes = 3;`) to match Unity/C# convention
- **Enums**: PascalCase for both the enum type and its members (`enum EnemyType {
  Citizen, Chef, WaffleIron }`)
- **Files**: filename matches the primary class name exactly (`WaffleWallet.cs`
  contains `class WaffleWallet`)
- **Scenes**: `PascalCase_WithContext` (`Runner_Level1`, `Arena_WaffleIron`)

## Architecture Patterns

### State Machine (Runner ↔ Kitchen-Approach ↔ Arena)
The game has a three-state loop per level: **Runner** (open scrolling/free-move
lane), **Kitchen-Approach** (still runner-like movement, but gated by the Chef
warning system), and **Arena** (fixed-camera pocket room for boss fights and,
later, cooking). Implement this as an explicit state machine (a `GameStateManager`
or similar), not as scattered boolean flags across scripts. Each state should be
its own class/component implementing a shared interface (e.g., `IGameState` with
`Enter()`, `Exit()`, `Tick()`), so adding new states later (new bosses, new kitchen
types) doesn't require touching unrelated code.

### Waffle Wallet System
Every character that can hold/drop waffles (citizens, later enemy types, the
player) should use a shared `WaffleWallet` component — don't duplicate
wallet-draining logic per enemy type. Wallet capacity, drop-on-defeat behavior, and
"sit and cry" disengage behavior should all read from this shared component,
configured per-instance via ScriptableObject data (see below), not hardcoded per
prefab.

### Tunable Values → ScriptableObjects
Any number a designer (you) would want to tweak without touching code should live
in a ScriptableObject, not a magic number in a script. This includes (non-
exhaustive, expand as needed):
- Citizen type stats (wallet size, hit strength, speed) — one SO per citizen type
- Waffle Iron attack timing/damage
- Chef schedule parameters (known-return windows, random-return probability,
  bedtime), blink-rate curve, fake-out frequency
- Recipe definitions (topping combos → life bonus / score multiplier)
- Stash capacity tiers (base + per-level-up increments)

Scripts should reference these via serialized fields, not instantiate or hardcode
values inline.

### Singletons — use sparingly
Only `GameStateManager` (and similar genuinely-one-per-game systems, e.g. a
top-level audio manager if one emerges) should be a singleton. Don't reach for
singletons as a default pattern — most systems (wallets, wallets' owning
characters, pickups) should be plain components referenced explicitly or found via
the state manager.

## Design Doc Sync

Whenever a design decision is finalized in conversation (with me, in chat), it
should get written into `/Docs/GameDesign.md` before or alongside the code that
implements it. If you ask Claude Code to build a system and the relevant design
decision isn't yet in that doc, Claude Code should ask rather than guess at
unspecified behavior (e.g., exact hit-strength-to-waffle-loss ratios, exact Chef
schedule timing) — these are gameplay-balance decisions, not implementation
details, and shouldn't be invented silently.

## Things to Avoid

- No magic numbers for anything gameplay-tunable — use ScriptableObjects (see above)
- No duplicated wallet/drop logic per enemy type — extend the shared `WaffleWallet`
  component instead
- Don't hardcode scene names as strings scattered across scripts — centralize scene
  references (e.g., a small constants class or SO listing scene names) so renaming
  a scene doesn't require a project-wide search
- Don't silently invent gameplay balance numbers (damage values, timing windows,
  drop rates) — flag these as open questions rather than guessing
- Avoid deep prefab nesting/inheritance chains for enemies — prefer composition
  (shared components like `WaffleWallet`, `EnemyMovement`) over enemy-type class
  hierarchies

## Avoiding Duplicate Code & Bloat

This project is being built largely by prompting an AI agent (Claude Code) rather
than hand-written incrementally, which makes duplication easy to introduce by
accident — each new request can get solved with fresh code instead of reusing what
already exists. Actively guard against this:

- **Before writing a new method or component, search the codebase for something
  that already does it (or close to it).** If a similar system exists (e.g., a new
  enemy type that's 90% like an existing one), extend/parameterize the existing
  component rather than copy-pasting it into a new file with small edits.
- **Shared behavior belongs in shared components, not repeated per-object.** This
  is why `WaffleWallet` is a single shared component (see above) rather than each
  enemy type having its own wallet logic — apply the same principle to any future
  system where multiple objects need the same underlying behavior (movement,
  hit-reaction, state transitions, UI update patterns, etc.).
- **If the same logic appears in two or more places, stop and extract it** into a
  shared method, base component, or utility class before adding a third copy. Two
  occurrences is the signal to refactor, not a threshold to wait past.
- **Prefer extending an existing script's responsibility over creating a new
  near-duplicate script**, but keep single-responsibility in mind — "extend" means
  adding a configurable parameter or a new branch of existing behavior, not
  stuffing unrelated systems into one bloated class. If a class starts handling
  clearly unrelated concerns, that's a sign to split it, not evidence that
  extending was wrong.
- **Periodically ask Claude Code to review recently added scripts for duplication**
  against the existing codebase, especially after several sessions of adding new
  enemy types, pickups, or UI screens — this is cheap to do and catches drift early
  before it compounds across many files.
- **Delete dead code rather than commenting it out.** Commented-out blocks
  accumulate as bloat and create ambiguity about whether they're needed. Version
  control (git) is the safety net for recovering old code, not in-file comments.
- **Watch for "one-off" scripts that quietly become permanent.** If a quick test
  script or throwaway prototype ends up staying in the project, either clean it up
  to match project standards or remove it — don't let temporary code linger
  alongside the real systems.

## Git Workflow

- **Never commit directly to `main`.** All work happens on a branch, merged via
  pull request after review/testing — even for a solo project, this keeps `main`
  always in a known-working state you can safely build from or roll back to.
- **Branch naming**: `feature/short-description` for new systems (`feature/waffle-
  wallet`, `feature/chef-schedule`), `fix/short-description` for bug fixes,
  `refactor/short-description` for cleanup work with no behavior change.
- **One branch per logical unit of work.** Don't let a single branch accumulate
  multiple unrelated systems (e.g., don't build the Chef schedule and the cooking
  UI in the same branch) — this keeps PRs reviewable and makes it easy to isolate
  what broke if something did.
- **Before merging to `main`:**
  1. The feature actually runs in the Unity Editor without errors
  2. Any tests written for the change (see Testing below) pass
  3. A quick read-through of the diff for duplication/bloat per the section above
- **Commit messages**: short present-tense summary line (`Add WaffleWallet drain
  logic`, `Fix Chef silhouette timing at return midpoint`), with a body only if the
  "why" isn't obvious from the diff alone.
- **Tag or note stable milestones** (e.g., "Level 1 playable end-to-end") so you
  have clear rollback points as the project grows, even if you're not doing formal
  releases yet.
- Claude Code should create a new branch itself before starting a new feature/fix
  if one isn't already checked out and appropriately named — don't build new work
  directly on `main` even if not explicitly told to branch first.

## Comments & Documentation

- Public methods/classes that aren't self-explanatory get a short `///` XML doc
  comment explaining intent, not just restating the signature
- Inline comments should explain *why*, not *what* — the code should already say
  what it does
- Any workaround for a Unity-specific quirk or platform limitation gets a comment
  explaining the constraint, so it doesn't get "cleaned up" by accident later

## Testing

Write tests alongside the code that needs them, not as an afterthought — when
Claude Code implements a new system, it should also write the corresponding
test(s) in the same session/PR, not defer them.

- **Framework**: Unity Test Framework (com.unity.test-framework), NUnit-based —
  add this package if not already present.
- **EditMode tests** for pure logic that doesn't need the game actually running:
  wallet math (waffle loss on hit, drain-to-zero → disengage), recipe/combo
  calculations, stash capacity limits, score tallying. Most of this project's core
  systems are good EditMode candidates since they're data/logic-driven rather than
  physics- or rendering-dependent.
- **PlayMode tests** for behavior that needs the actual scene/game loop running:
  state machine transitions (Runner → Kitchen-Approach → Arena), collision-driven
  pickup/hit detection, the Chef's schedule/warning-stack timing.
- **What to prioritize testing**: anything with numeric logic or conditional
  branching that would be easy to silently get wrong — wallet drain math, hit-
  strength-to-waffle-loss scaling, the 3-strike citizen system, blink-rate/fake-out
  timing, recipe bonus calculations. Pure visual/animation polish doesn't need
  test coverage; game-balance-critical logic does.
- **Tests live alongside the systems they cover**: a `Tests/` folder mirroring the
  `Scripts/` structure (e.g., `Assets/Scripts/Waffles/Tests/WaffleWalletTests.cs`),
  or a top-level `Assets/Tests/` split into `EditMode/` and `PlayMode/` — pick one
  convention and apply it consistently rather than mixing approaches.
- Tests should fail loudly and specifically (assert the actual expected number,
  not just "no exception thrown") so a broken test tells you what's wrong, not
  just that something is.
- This isn't about exhaustive coverage — it's about making sure the gameplay-
  balance math (the stuff that's easy to get subtly wrong and hard to notice by
  eyeballing the game) is verified automatically as the project grows.
