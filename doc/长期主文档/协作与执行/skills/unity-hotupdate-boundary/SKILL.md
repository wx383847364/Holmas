---
name: unity-hotupdate-boundary
description: Use for this Unity + HybridCLR + YooAssets project when implementing or reviewing gameplay code, architecture boundaries, cross-layer DTOs, runtime asset loading, or subagent task splits. Enforces App.AOT/App.Shared/App.HotUpdate ownership, keeps gameplay logic out of UI and AOT, and treats MinesweeperTerrainData as a board template instead of runtime state.
---

# Unity HotUpdate Boundary

Use this skill for any task that touches:
- `App.Shared`
- `App.AOT`
- `App.HotUpdate`
- YooAssets loading
- `MinesweeperTerrainData`
- gameplay architecture
- subagent task assignment for this project

## Goal

Keep the project structurally stable while gameplay is added.
Prioritize clear ownership over local convenience.

## Core Rules

- `App.AOT` is host infrastructure only.
- `App.AOT` may initialize services, persistence, platform bridges, YooAssets, HybridCLR, and shared infrastructure.
- `App.AOT` must not contain find-cat gameplay rules, board generation, task generation, reward formulas, or UI business flow.

- `App.Shared` is for stable cross-layer contracts only.
- Put only DTOs, interfaces, and cross-layer events in `App.Shared`.
- Do not put gameplay service implementations, Unity scene logic, or `ScriptableObject` logic in `App.Shared`.

- `App.HotUpdate` is the gameplay layer.
- Put gameplay services, board logic, task logic, progression logic, orchestration, and presenters here.
- Prefer pure C# domain logic that can be tested without Unity scene objects.

- `MonoBehaviour` is presentation glue, not gameplay authority.
- UI classes may forward input, bind state, and refresh visuals.
- UI classes must not own reward calculation, task generation, board generation, or persistence rules.

- `MinesweeperTerrainData` is a static board template.
- It may define rows, cols, valid cells, and block colors.
- It must not hold runtime mission progress, spawned cats, rewards, or save state.

- Runtime assets must load through YooAssets in formal gameplay flows.
- Do not introduce new `Resources.Load` calls for formal runtime features.
- Do not use editor-only APIs in runtime gameplay code.

## Required Workflow

When implementing a feature:
1. Identify which layer owns the change.
2. Freeze or confirm DTO/interface changes before parallel work.
3. Keep domain logic in pure C# services and models.
4. Keep Unity object code thin.
5. Verify resource access path and persistence ownership.
6. Review for boundary violations before finishing.

## Ownership Guide

Use these defaults:
- `App.Shared`: only when a type must cross AOT/HotUpdate boundary.
- `App.AOT`: only when integrating platform, patching, boot, persistence, or shared infrastructure.
- `App.HotUpdate`: default home for feature work.

If unsure, prefer `App.HotUpdate`.

## Project-Specific Decisions

For this project:
- `MinesweeperTerrainData` is a map template input.
- Runtime board state should live in `BoardTemplate`, `LevelSnapshot`, `TaskInstanceData`, `TaskSlotState`, or equivalent runtime DTOs.
- Task generation depends on player-level config and current task slot rules, not on terrain assets.
- Terrain color is visual input only, not gameplay authority.
- Reward formulas belong to gameplay services, not UI.
- Ad unlock timers and offline settlement belong to time, persistence, or progression services, not presenters.
- For new names in this project, avoid `meta` in file names, folder names, type names, and variable names.
- Prefer domain-specific names such as `progression`, `progress`, `agency`, `catalog`, `definition`, `state`, or the concrete gameplay term.
- Unity-generated `.meta` files are not part of this naming rule.
- Keep `meta` only when matching an external contract, third-party API, or an already-fixed upstream identifier.

## Subagent Rules

When splitting work across subagents:
- Every subagent should inherit this skill.
- Only one agent may lead changes in `App.Shared`.
- Only one agent may lead changes in HotUpdate entry or composition root.
- Only one UI-focused agent should change prefabs or scene bindings.
- Freeze DTO names before parallel implementation if multiple agents depend on shared data.

## Do Not

- Do not place gameplay rules in `App.AOT`.
- Do not place runtime mutable state in config assets.
- Do not put gameplay formulas in UI scripts.
- Do not bypass YooAssets for formal runtime content.
- Do not let multiple agents edit `App.Shared` casually.
- Do not expand prototype `Minesweeper` UI scripts directly into the final production architecture without refactoring ownership.

## Validation

Before finishing:
- Check layer ownership.
- Check runtime/editor API separation.
- Check that Unity views stay thin.
- Check that mutable runtime state is not stored in template or config assets.
- Check that cross-layer types in `App.Shared` are minimal and stable.

Read these references when needed:
- `references/boundaries.md`
- `references/checklist.md`

Run this for boundary-sensitive changes:
- `tools/validation/check_boundary.sh`
