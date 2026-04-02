# Boundary Reference

## Layer Ownership

### `App.AOT`

Owns:
- bootstrap
- platform bridge
- persistence provider
- YooAssets runtime
- HybridCLR loading
- shared infrastructure

Does not own:
- board generation
- task generation
- reward formulas
- detective agency progression
- UI business flow

### `App.Shared`

Owns:
- stable cross-layer interfaces
- DTOs that must move between AOT and HotUpdate
- shared event types

Does not own:
- gameplay service implementations
- Unity scene logic
- `MonoBehaviour`
- `ScriptableObject`
- mutable runtime state logic

### `App.HotUpdate`

Owns:
- gameplay services
- orchestration
- pure C# domain models
- task flow
- board and map logic
- progression systems
- presenters and feature composition

## Runtime State Model

Use this fixed interpretation:

- `MinesweeperTerrainData`
  - static board template
  - rows, cols, valid cells, block colors

- `BoardTemplate`
  - runtime-safe projection of terrain template
  - pure data used by gameplay logic

- `LevelSnapshot`
  - mutable runtime level state
  - spawned cats, revealed cells, completion, seed, level-specific state

- `TaskInstanceData` / `TaskSlotState`
  - mutable task runtime state

Do not store `LevelSnapshot` or task progress inside `MinesweeperTerrainData`.

## Typical Violations

Avoid these patterns:

- Generating tasks inside UI presenters
- Putting reward formulas inside `MonoBehaviour`
- Saving runtime progress back into config or template assets
- Adding gameplay services to `App.AOT`
- Putting Unity object types into `App.Shared`
- Loading formal runtime assets with `Resources.Load` when YooAssets is the intended path

## Safe Heuristic

If a change includes gameplay rules, mutable runtime state, or feature orchestration, default to `App.HotUpdate`.

If a change only exists to cross the AOT/HotUpdate boundary, consider `App.Shared`.

If a change only exists to boot, patch, bridge, persist, or host the app, consider `App.AOT`.
