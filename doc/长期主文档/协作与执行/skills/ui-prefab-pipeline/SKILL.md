---
name: ui-prefab-pipeline
description: Use for this project when defining, reviewing, implementing, or validating UI prefab generation and UI business binding flow from DesignPacket to UiPrefabSpec to PrefabBindingManifest and validation. Enforces spec authority, generation boundaries, static bindings, prefab visual preservation, manifest structure, trial samples, and deterministic regression expectations.
---

# UI Prefab Pipeline

Use this skill for any task that touches:
- `DesignPacket`
- `UiPrefabSpec`
- `PrefabBindingManifest`
- generator flow
- validator flow
- UI prefab binding flow for runtime View/Controller logic
- golden cases
- deterministic regression
- sample spec or sample manifest outputs

## Goal

Keep the pipeline explicit, reviewable, and deterministic.
Treat `UiPrefabSpec` as the only machine-authoritative intermediate layer.

## Core Rules

- Images are design references, not machine authority.
- `UiPrefabSpec` is the only machine-authoritative intermediate spec.
- First version supports Unity UGUI only.
- First version outputs prefab drafts and `PrefabBindingManifest` only.
- The system must not auto-write business Presenter logic or gameplay wiring.
- Validation must catch illegal nodes, illegal components, missing asset slots, and naming conflicts before promotion.
- A local spec change should only cause local output drift.
- 运行时 UI 业务代码只能通过 `UiReferenceCollector` / generated bindings / manifest 取得节点；禁止用 `Transform.Find`、`GameObject.Find`、递归 `GetComponentsInChildren` 等查找式取节点作为功能实现。
- 生成器必须把 View/Controller 会触碰的全部节点、组件、事件出口、状态层、隐藏 slot 和锁定态写进 manifest 与静态绑定；缺绑定应作为生成/审核失败处理。
- 生成、验证或业务接入 UI prefab 时，默认不得改变原 prefab 的颜色、透明度、`CanvasGroup.alpha`、`Graphic.color`、材质颜色或默认 tint。只有在用户明确要求视觉变化，或已批准的 `UiPrefabSpec` 明确记录该视觉变化时，才允许改动。

## Pipeline Boundaries

- `DesignPacket` captures design input, states, rules, asset-slot hints, and notes.
- `UiPrefabSpec` captures structure, components, bindings, resource slots, and event exits.
- `PrefabBindingManifest` captures what was generated and what still needs manual wiring.
- `ProjectUiProfile` is project-facing configuration and belongs to adapter work, not to core schema ownership.

## Skill Trigger Guide

Reach for this skill when the user asks things like:
- “补 DesignPacket 和 UiPrefabSpec 的契约”
- “规划 prefab 草稿生成和 manifest 输出”
- “给 UI 自动生成系统补 validator 和 deterministic 回归规则”
- “给已有 prefab 接 UI 业务逻辑/静态绑定/状态出口”
- “检查 UI 业务接入有没有乱改 prefab 颜色或透明度”

## Required Workflow

1. Normalize design input into `DesignPacket`.
2. Convert it into an explicit `UiPrefabSpec`.
3. Require human review before generation.
4. Generate prefab draft and manifest from the approved spec.
5. Run validation and regression before any promotion or business-side adoption.

## Trial Sample Rules

- `Samples~/Holmas` is for trial inputs, sample manifests, adapter samples, and golden fixtures.
- `Documentation~` should only hold subsystem-specific notes, not duplicate the main long-term docs.
- `Subagent 5` should not fully engage until there is at least one approved sample spec and one sample manifest.

## Do Not

- Do not skip directly from image to formal prefab.
- Do not mix spec, generation, and validation into the same ownership area.
- Do not let generator code depend on Holmas gameplay logic.
- Do not let adapter policy redefine core schema.
- Do not leave runtime node lookup as a convenience fallback. One-time editor migration/authoring scripts may use explicit路径补齐静态绑定，但必须输出到 prefab/collector 后再交付，不能让页面打开时再查找节点。
- Do not treat visual cleanup as part of binding or business-logic work. Existing prefab colors and transparency are source visuals and must be preserved unless explicitly requested or explicitly present in the approved spec.

## Validation

Before finishing:
- Check that generator and validator consume `UiPrefabSpec`, not raw images.
- Check that the manifest fully describes generated nodes, components, asset slots, and manual wiring gaps.
- Check that runtime View/Controller code consumes static bindings only, and that every referenced UI node has a manifest/collector entry.
- Check that generated or modified prefabs preserve original colors, transparency, tint defaults, material colors, and `CanvasGroup.alpha` unless the approved task/spec explicitly requests a visual change.
- Check that there is at least one repeatable sample path for validation.
- Check that failure reporting distinguishes schema, generator, adapter, and fixture issues.

Read these references when needed:
- `references/contracts.md`
- `references/review-checklist.md`
- `references/golden-cases.md`
