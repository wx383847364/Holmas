# Boundary Checklist

## Before Coding

- Which layer owns this change: `App.AOT`, `App.Shared`, or `App.HotUpdate`?
- Does this task really require a new shared DTO or interface?
- Is the resource path supposed to be a YooAssets key?
- Will this feature create mutable runtime state?
- Can the core logic stay pure C# without Unity scene objects?

## Before Parallel Work

- Are DTO names frozen?
- Is ownership of `App.Shared` explicit?
- Is ownership of HotUpdate entry or composition root explicit?
- Is ownership of UI prefabs explicit?

## Before Finishing

- Did any gameplay rule leak into `App.AOT`?
- Did any Unity object logic leak into `App.Shared`?
- Did any reward or generation logic leak into UI scripts?
- Did any runtime mutable state leak into config or template assets?
- Did this feature bypass YooAssets for formal runtime content?
- Did any runtime code accidentally use `UnityEditor`?
- Did any new project-owned name unnecessarily include `meta`?

## Project-Specific Reminders

- `MinesweeperTerrainData` is only a map template.
- Terrain color is presentation input, not gameplay authority.
- Task generation depends on player-level config and task slot rules.
- Reward formulas belong in gameplay services.
- Ad unlock timing and offline settlement belong in time, persistence, or progression services.
